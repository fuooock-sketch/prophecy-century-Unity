FROM python:3.11-slim

WORKDIR /app
COPY tools/casual_pvp_server /app/tools/casual_pvp_server
COPY Assets/Resources/Data /app/Assets/Resources/Data

RUN useradd --system --uid 10001 pvp \
    && mkdir -p /var/lib/prophecy-century-casual-pvp \
    && chown -R pvp:pvp /app /var/lib/prophecy-century-casual-pvp

USER pvp
EXPOSE 8765
ENTRYPOINT ["python", "/app/tools/casual_pvp_server/server.py"]
