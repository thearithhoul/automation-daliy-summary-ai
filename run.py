import uvicorn

if __name__ == "__main__":
    # reload=True forces uvicorn onto SelectorEventLoop on Windows, which can't
    # spawn subprocesses (breaks Playwright). Keep reload off here.
    uvicorn.run("src.main:app", host="0.0.0.0", port=8000, reload=False)
