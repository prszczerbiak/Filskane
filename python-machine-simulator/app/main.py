import asyncio
import contextlib
import json
import math
import os
from datetime import datetime, timezone


DEFAULT_HOST = os.getenv("SIMULATOR_HOST", "0.0.0.0")
DEFAULT_PORT = int(os.getenv("SIMULATOR_PORT", "8001"))
DEFAULT_TYPE = os.getenv("SIMULATOR_MACHINE_TYPE", "tractor")
DEFAULT_LAT = float(os.getenv("SIMULATOR_LAT", "50.800667"))
DEFAULT_LNG = float(os.getenv("SIMULATOR_LNG", "19.124278"))
DEFAULT_INTERVAL = float(os.getenv("SIMULATOR_INTERVAL_SECONDS", "0.1"))


def _build_route(lat: float, lng: float) -> list[tuple[float, float]]:
    half_height_m = float(os.getenv("SIMULATOR_HALF_HEIGHT_M", "60"))
    half_width_m = float(os.getenv("SIMULATOR_HALF_WIDTH_M", "100"))
    dlat = half_height_m / 111_000.0
    dlng = half_width_m / (111_000.0 * max(math.cos(math.radians(lat)), 0.1))

    return [
        (lat + dlat, lng - dlng),
        (lat + dlat, lng + dlng),
        (lat - dlat, lng + dlng),
        (lat - dlat, lng - dlng),
    ]


async def _read_initial_config(reader: asyncio.StreamReader) -> dict[str, object]:
    try:
        raw = await asyncio.wait_for(reader.readline(), timeout=0.2)
    except TimeoutError:
        return {}

    if not raw:
        return {}

    try:
        payload = json.loads(raw.decode("utf-8").strip())
    except json.JSONDecodeError:
        return {}

    if not isinstance(payload, dict):
        return {}

    return payload


async def handle_client(reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
    config = await _read_initial_config(reader)

    machine_type = str(config.get("type") or DEFAULT_TYPE)
    lat = float(config.get("lat") or DEFAULT_LAT)
    lng = float(config.get("lng") or DEFAULT_LNG)
    interval = float(config.get("interval") or DEFAULT_INTERVAL)

    route = _build_route(lat, lng)
    edge_idx = 0
    t = 0.0
    step = 0.02

    try:
        while True:
            a = route[edge_idx]
            b = route[(edge_idx + 1) % 4]

            curr_lat = a[0] + (b[0] - a[0]) * t
            curr_lng = a[1] + (b[1] - a[1]) * t

            message = {
                "type": machine_type,
                "lat": curr_lat,
                "lng": curr_lng,
                "timestamp": datetime.now(timezone.utc).isoformat(),
            }

            writer.write((json.dumps(message, separators=(",", ":")) + "\n").encode("utf-8"))
            await writer.drain()

            t += step
            if t >= 1.0:
                t = 0.0
                edge_idx = (edge_idx + 1) % 4

            await asyncio.sleep(interval)
    except (ConnectionResetError, BrokenPipeError, asyncio.CancelledError):
        pass
    finally:
        writer.close()
        with contextlib.suppress(Exception):
            await writer.wait_closed()


async def main() -> None:
    server = await asyncio.start_server(handle_client, DEFAULT_HOST, DEFAULT_PORT)
    addrs = ", ".join(str(sock.getsockname()) for sock in server.sockets or [])
    print(f"Filskane machine simulator listening on {addrs}")

    async with server:
        await server.serve_forever()


if __name__ == "__main__":
    asyncio.run(main())
