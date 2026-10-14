# Toolbox de validación de infraestructura generado por keel-dotnet.
# Solo trae las CLIs del stack elegido (keel-stack.json). Sin puertos: es un
# objetivo interno de `docker exec`, no un servicio expuesto.
FROM alpine:3.20
RUN apk add --no-cache bash curl jq netcat-openbsd postgresql-client
WORKDIR /scripts
CMD ["sleep", "infinity"]
