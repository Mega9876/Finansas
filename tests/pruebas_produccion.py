#!/usr/bin/env python3
"""Comprueba configuración Production con una base inaccesible, sin tocar los datos locales."""
import json
import subprocess
import time
import urllib.error
import urllib.request
import uuid


def request(port, path):
    try:
        response = urllib.request.urlopen(f"http://127.0.0.1:{port}{path}", timeout=5)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.status, response.read().decode()


def main():
    name = "finanzas-test-production-" + uuid.uuid4().hex[:10]
    subprocess.run([
        "docker", "run", "-d", "--rm", "--name", name,
        "-p", "127.0.0.1::8080",
        "-e", "ASPNETCORE_ENVIRONMENT=Production",
        "-e", "Database__ApplyMigrations=true",
        "-e", "ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=1;Database=inaccesible;Timeout=1",
        "finanzas-personales-api:local"
    ], check=True, capture_output=True, text=True)
    try:
        binding = subprocess.check_output(["docker", "port", name, "8080/tcp"], text=True).strip()
        port = int(binding.rsplit(":", 1)[1])
        for attempt in range(30):
            try:
                status, _ = request(port, "/scalar")
                break
            except OSError:
                if attempt == 29:
                    raise
                time.sleep(1)
        if status != 404 or request(port, "/openapi/v1.json")[0] != 404:
            raise AssertionError("Scalar/OpenAPI deben estar deshabilitados en Production")
        print("OK: Production inicia aunque la BD sea inaccesible y ApplyMigrations=true; no migra automáticamente")
        print("OK: Scalar y OpenAPI devuelven 404 en Production")
        status, body = request(port, "/api/usuarios")
        problem = json.loads(body)
        if status != 500 or problem["status"] != 500 or any(
                value in body for value in ["Npgsql", "StackTrace", "ConnectionString", "inaccesible"]):
            raise AssertionError("Los errores deben ser ProblemDetails sin detalles internos")
        print("OK: Fallo de BD devuelve 500 ProblemDetails sin detalles internos")
        uid = subprocess.check_output(["docker", "exec", name, "id", "-u"], text=True).strip()
        if uid == "0":
            raise AssertionError("El contenedor no debe ejecutarse como root")
        print(f"OK: Contenedor ejecutado sin privilegios (UID={uid})")
    finally:
        subprocess.run(["docker", "stop", name], check=True, capture_output=True)


if __name__ == "__main__":
    main()
