from fastapi import FastAPI, HTTPException

from app.analysis import run_dbscan, run_multi_index_grouping
from app.schemas import (
    DbscanRequest,
    DbscanResponse,
    MultiIndexGroupingRequest,
    MultiIndexGroupingResponse,
)

app = FastAPI(title="Filskane Python Analysis Service", version="1.0.0")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/dbscan", response_model=DbscanResponse)
def dbscan(request: DbscanRequest) -> DbscanResponse:
    try:
        return run_dbscan(request)
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc


@app.post("/multi-index-grouping", response_model=MultiIndexGroupingResponse)
def multi_index_grouping(request: MultiIndexGroupingRequest) -> MultiIndexGroupingResponse:
    try:
        return run_multi_index_grouping(request)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc
