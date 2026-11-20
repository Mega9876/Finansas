#!/usr/bin/env python3
"""Pruebas reales contra Compose; crea datos de prueba y nunca borra filas ni volúmenes."""
import argparse
import concurrent.futures
import json
from pathlib import Path
import re
import subprocess
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
BASE = "http://localhost:8080"
COMPOSE = ["docker", "compose", "-f", "constructor.yml"]
PASSWORD = "ClaveDePrueba123!"  # Dato ficticio exclusivo de las pruebas.


def check(condition, description):
    if not condition:
        raise AssertionError(description)
    print(f"OK: {description}")


def request(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method,
                                 headers={"Content-Type": "application/json"})
    try:
        response = urllib.request.urlopen(req, timeout=45)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        text = response.read().decode()
        content_type = response.headers.get("Content-Type", "")
        return response.status, json.loads(text) if "json" in content_type and text else text


def wait_api():
    deadline = time.monotonic() + 90
    while time.monotonic() < deadline:
        try:
            if request("GET", "/api/usuarios")[0] == 200:
                return
        except (OSError, TimeoutError):
            pass
        time.sleep(1)
    raise RuntimeError("La API no respondió en 90 segundos.")


def database_rows(ids):
    # Solo SELECT de comprobación; todo el esquema procede de migraciones EF.
    safe_ids = [str(uuid.UUID(value)) for value in ids]
    literals = ",".join(f"'{value}'::uuid" for value in safe_ids)
    query = ("SELECT coalesce(json_agg(t ORDER BY id), '[]'::json) FROM "
             f"(SELECT * FROM usuario WHERE id IN ({literals})) t;")
    result = subprocess.run(COMPOSE + ["exec", "-T", "postgres", "sh", "-c",
                            'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "$1"',
                            "sh", query], cwd=ROOT, check=True, capture_output=True, text=True)
    return json.loads(result.stdout)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reiniciar-contenedores", action="store_true",
                        help="Ejecuta down/up sin -v y verifica persistencia (interrumpe la API local).")
    args = parser.parse_args()
    wait_api()
    status, html = request("GET", "/scalar")
    check(status == 200 and "scalar" in html.lower(), "Scalar sirve su página HTML")
    status, document = request("GET", "/openapi/v1.json")
    operations = {(path, method) for path, item in document["paths"].items()
                  for method in item if method in {"get", "post", "delete", "put", "patch"}}
    check(status == 200 and operations == {
        ("/api/usuarios", "get"), ("/api/usuarios", "post"),
        ("/api/usuarios/{id}", "delete")}, "OpenAPI documenta exactamente los tres endpoints")
    status, users = request("GET", "/api/usuarios")
    check(status == 200 and isinstance(users, list), "GET devuelve 200 y una lista")
    if not users:
        check(users == [], "GET sin usuarios activos devuelve []")
    for user in users:
        check(set(user) == {"id", "nombre", "email", "fechaCreacion"}, "GET solo expone campos públicos")

    token = uuid.uuid4().hex
    email = f"prueba.{token}@ejemplo.com"
    payload = {"nombre": "Usuario Prueba", "email": email.upper(), "password": PASSWORD}
    status, created = request("POST", "/api/usuarios", payload)
    check(status == 201 and created["email"] == email, "POST devuelve 201 y normaliza mayúsculas")
    check(set(created) == {"id", "nombre", "email", "fechaCreacion"}, "POST no expone contraseña ni hash")
    check(created["fechaCreacion"].endswith("Z"), "FechaCreacion está en UTC")
    user_id = created["id"]
    row = database_rows([user_id])[0]
    check(row["activo"] is True and row["email"] == email, "POST persiste un usuario activo en PostgreSQL")
    check(re.match(r"^\$2[aby]\$12\$", row["password_hash"]) is not None
          and PASSWORD not in row["password_hash"], "PostgreSQL contiene únicamente un hash BCrypt con coste 12")
    check(request("POST", "/api/usuarios", payload)[0] == 409, "Correo duplicado devuelve 409")

    invalid_values = [
        {"nombre": "   "}, {"nombre": None}, {"nombre": "x" * 101},
        {"email": "no-es-correo"}, {"email": "a" * 151 + "@ejemplo.com"},
        {"password": "corta"}, {"password": " " * 12}, {"password": "á" * 37},
        {"password": "x" * 73}, {"password": None}, {"password": "abc\0defghijklmnop"}
    ]
    check(all(request("POST", "/api/usuarios", payload | changes)[0] == 400
              for changes in invalid_values), "Validaciones de nombre, email y contraseña devuelven 400")
    check(request("POST", "/api/usuarios", {})[0] == 400, "Campos requeridos devuelven 400")
    check(request("DELETE", f"/api/usuarios/{uuid.uuid4()}")[0] == 404, "DELETE inexistente devuelve 404")
    check(request("DELETE", f"/api/usuarios/{user_id}")[0] == 204, "DELETE activo devuelve 204")
    check(request("DELETE", f"/api/usuarios/{user_id}")[0] == 204, "DELETE repetido es idempotente: 204")
    check(all(user["id"] != user_id for user in request("GET", "/api/usuarios")[1]),
          "GET excluye al usuario desactivado")
    row = database_rows([user_id])[0]
    check(row["activo"] is False, "La fila permanece en PostgreSQL con activo=false")
    check(request("POST", "/api/usuarios", payload)[0] == 409, "El email de un usuario inactivo sigue reservado")

    concurrent_email = f"concurrencia.{token}@ejemplo.com"
    def register(index):
        return request("POST", "/api/usuarios", payload | {
            "email": concurrent_email.upper() if index % 2 else concurrent_email})
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        results = list(pool.map(register, range(6)))
    statuses = [status for status, _ in results]
    check(statuses.count(201) == 1 and statuses.count(409) == 5,
          "Seis POST concurrentes: un 201 y cinco 409, sin errores 500")
    active_id = next(body["id"] for status, body in results if status == 201)
    rows = database_rows([user_id, active_id])
    check(len(rows) == 2 and rows[0]["password_hash"] != rows[1]["password_hash"],
          "La misma contraseña genera hashes distintos mediante salt aleatorio")

    logs = subprocess.check_output(COMPOSE + ["logs", "--no-color", "api", "postgres"],
                                   cwd=ROOT, text=True, stderr=subprocess.STDOUT)
    check(PASSWORD not in logs and all(row["password_hash"] not in logs for row in rows),
          "Los logs de API y PostgreSQL no contienen la contraseña ni los hashes de prueba")

    if args.reiniciar_contenedores:
        subprocess.run(COMPOSE + ["down"], cwd=ROOT, check=True)
        subprocess.run(COMPOSE + ["up", "-d", "--wait", "--wait-timeout", "180"], cwd=ROOT, check=True)
        wait_api()
        check(database_rows([user_id, active_id]) == rows,
              "Tras down/up permanecen ambas filas, hashes y estado activo/inactivo")
        listed_ids = {user["id"] for user in request("GET", "/api/usuarios")[1]}
        check(active_id in listed_ids and user_id not in listed_ids,
              "GET conserva el comportamiento de soft delete después de recrear contenedores")

    print("Pruebas completadas. Se conservan un usuario activo y uno inactivo de prueba.")


if __name__ == "__main__":
    main()
