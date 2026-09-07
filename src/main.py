from typing import Annotated

from fastapi import Depends, FastAPI

from src.auth import require_token
from src.scraping import router as scraping_router

app = FastAPI(title="setup-craw14ia")

app.include_router(scraping_router)


@app.get("/")
async def root() -> dict:
    return {"message": "setup-craw14ia is running"}


@app.get("/protected")
async def protected(payload: Annotated[dict, Depends(require_token)]) -> dict:
    return {"message": "authorized", "sub": payload.get("sub")}
