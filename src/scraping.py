
from crawl4ai import AsyncWebCrawler
from fastapi import APIRouter, Depends, HTTPException
from src.auth import require_token
from src.markdown_parser import Article, parse_articles
from pydantic import BaseModel, HttpUrl

router = APIRouter(prefix="/scrape", tags=["scraping"])


class ScrapeRequest(BaseModel):
    url: HttpUrl


class ScrapeResponse(BaseModel):
    url: str
    articles: list[Article]
    articles_count: int


@router.post("", response_model=ScrapeResponse, dependencies=[Depends(require_token)])
async def scrape(request: ScrapeRequest) -> ScrapeResponse:
    async with AsyncWebCrawler() as crawler:
        result = await crawler.arun(url=str(request.url))

    if not result.success:
        raise HTTPException(status_code=502, detail=result.error_message)

    articles = parse_articles(result.markdown, page_metadata=result.metadata)
    
    return ScrapeResponse(
        url=str(request.url),
        articles=articles,
        articles_count=len(articles),
    )
