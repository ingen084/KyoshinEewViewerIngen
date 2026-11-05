"""EEW Client の外部連携機能を模した WebSocket サーバー
接続すると start / server-status を送り、その後 EEW を数報配信する
"""
import asyncio
import json
import sys
from datetime import datetime, timedelta, timezone

import websockets

JST = timezone(timedelta(hours=9))
PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 58325


def iso(dt: datetime) -> str:
    return dt.isoformat(timespec="milliseconds")


def eew(serial: int, now: datetime, with_hypo: bool, s_wave_ok: bool, canceled: bool = False, last: bool = False):
    msg = {
        "type": "eew",
        "eventId": "20260914013000",
        "serial": str(serial),
        "author": "気象庁本庁",
        "pressTime": iso(now),
        "processTime": iso(now),
        "isCanceled": canceled,
        "isLastInfo": last,
        "isPlumOnly": not with_hypo,
        "isLevelOver": False,
        "isWarning": with_hypo,
        "epicenterName": "石川県能登地方" if with_hypo else "富山湾",
        "epicenterLocation": [137.2, 37.5] if with_hypo else [137.4, 37.4],
        "depth": 10,
        "magnitude": 5.7 if with_hypo else None,
        "originTime": iso(now - timedelta(seconds=8)),
        "arrivalTime": iso(now - timedelta(seconds=6)),
        "accuracy": {"epicenters": ["3", "3"], "depth": "3", "magnitudeCalculation": "2", "numberOfMagnitudeCalculation": "4"},
        "regions": [{"code": "390", "maxInt": "5-"}, {"code": "391", "maxInt": "3"}, {"code": "200", "maxInt": "1"}],
        "pointForecast": {
            "sWave": {"status": "ok" if s_wave_ok else "not-calculated", "time": iso(now + timedelta(seconds=40)) if s_wave_ok else None},
            "lpgm": {"maxSva": "0.04", "lgInt": "0"},
            "intensity": {"type": "attenuation", "k": "4.6" if with_hypo else None, "int": "5-" if with_hypo else None},
        },
    }
    if with_hypo:
        msg["maxInt"] = {"from": "5+", "to": "5+"}
        msg["maxLgInt"] = {"from": "1", "to": "1"}
    return msg


async def handler(ws):
    print("client connected", flush=True)
    await ws.send(json.dumps({"type": "start", "version": "1.4.0"}, ensure_ascii=False))
    # 予想地点の座標 [経度, 緯度] (未設定は None、共有しない場合は False)
    await ws.send(json.dumps({"type": "user-point", "location": [136.9066, 35.1815]}))
    # 受信不可の状態を経由してから受信可能にする
    await ws.send(json.dumps({"type": "server-status", "status": "no-auth"}))
    await asyncio.sleep(3)
    await ws.send(json.dumps({"type": "server-status", "status": "connecting"}))
    await asyncio.sleep(2)
    await ws.send(json.dumps({"type": "server-status", "status": "open"}))
    await asyncio.sleep(3)

    now = datetime.now(JST)
    # 1報目: PLUM のみ、地点予測なし
    await ws.send(json.dumps(eew(1, now, with_hypo=False, s_wave_ok=False), ensure_ascii=False))
    await asyncio.sleep(2)
    # 2報目: 震源確定、地点予測あり (2回配信して同一通番の再配信を模す)
    for _ in range(2):
        await ws.send(json.dumps(eew(2, datetime.now(JST), with_hypo=True, s_wave_ok=True), ensure_ascii=False))
        await asyncio.sleep(1)
    await asyncio.sleep(2)
    await ws.send(json.dumps(eew(3, datetime.now(JST), with_hypo=True, s_wave_ok=True, last=True), ensure_ascii=False))
    print("all messages sent", flush=True)
    try:
        await ws.wait_closed()
    finally:
        print("client disconnected", flush=True)


async def main():
    async with websockets.serve(handler, "127.0.0.1", PORT):
        print(f"listening on ws://127.0.0.1:{PORT}", flush=True)
        await asyncio.Future()


asyncio.run(main())
