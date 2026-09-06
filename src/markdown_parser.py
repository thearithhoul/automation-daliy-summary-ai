import re
from collections import Counter
from typing import Any

from pydantic import BaseModel

_IMAGE_RE = re.compile(r"!\[([^\]]*)\]\(([^)]+)\)")
_LINK_RE = re.compile(r"\[([^\]]*)\]\(([^)]+)\)")
_HEADING_RE = re.compile(r"^(#{1,6})\s+(.*)")
_LIST_MARKER_RE = re.compile(r"^\s*(?:[-*+]|\d+\.)\s+")
_HASHTAG_RE = re.compile(r"(?<!\w)#([A-Za-z][\w-]*)")


class LinkItem(BaseModel):
    text: str
    url: str


class Article(BaseModel):
    title: str | None
    subtitle: str | None
    links: list[LinkItem]
    metadata: dict[str, Any]
    other: str


def _strip_link_markup(text: str) -> str:
    text = _IMAGE_RE.sub(lambda m: m.group(1), text)
    return _LINK_RE.sub(lambda m: m.group(1), text)


def _line_is_pure_link(line: str) -> bool:
    stripped = _LIST_MARKER_RE.sub("", line).strip()
    if not stripped:
        return False
    match = _LINK_RE.fullmatch(stripped) or _IMAGE_RE.fullmatch(stripped)
    return match is not None


def _build_article_from_lines(lines: list[str], page_metadata: dict[str, Any] | None) -> Article:
    images = []
    tags: list[str] = []
    headings: list[str] = []
    other_lines: list[str] = []
    title: str | None = None
    subtitle: str | None = None

    joined = "\n".join(lines)
    text_without_images = _IMAGE_RE.sub("", joined)
    links = [LinkItem(text=m.group(1), url=m.group(2)) for m in _LINK_RE.finditer(text_without_images)]

    for m in _HASHTAG_RE.finditer(_strip_link_markup(joined)):
        tag = m.group(1)
        if tag not in tags:
            tags.append(tag)

    for raw_line in lines:
        for m in _IMAGE_RE.finditer(raw_line):
            images.append({"alt": m.group(1), "url": m.group(2)})

        heading_match = _HEADING_RE.match(raw_line.strip())
        if heading_match:
            text = heading_match.group(2).strip()
            headings.append(text)
            if title is None:
                title = text
            elif subtitle is None:
                subtitle = text
            else:
                other_lines.append("#" * len(heading_match.group(1)) + " " + text)
            continue

        if _line_is_pure_link(raw_line) or _IMAGE_RE.fullmatch(raw_line.strip()):
            continue

        other_lines.append(_strip_link_markup(raw_line))

    other = "\n".join(other_lines).strip()
    other = re.sub(r"\n{3,}", "\n\n", other)

    metadata: dict[str, Any] = dict(page_metadata or {})
    metadata["heading_count"] = len(headings)
    metadata["images"] = images
    if tags:
        metadata["tags"] = tags

    return Article(title=title, subtitle=subtitle, links=links, metadata=metadata, other=other)


def parse_articles(markdown: str, page_metadata: dict[str, Any] | None = None) -> list[Article]:
    lines = markdown.splitlines()
    heading_positions = []
    for i, raw_line in enumerate(lines):
        heading_match = _HEADING_RE.match(raw_line.strip())
        if heading_match:
            heading_positions.append((i, len(heading_match.group(1))))

    if not heading_positions:
        return [_build_article_from_lines(lines, page_metadata)]

    level_counts = Counter(level for _, level in heading_positions)
    item_level, item_count = level_counts.most_common(1)[0]

    if item_count < 2:
        return [_build_article_from_lines(lines, page_metadata)]

    item_indices = [i for i, level in heading_positions if level == item_level]
    boundaries = item_indices[1:] + [len(lines)]

    return [
        _build_article_from_lines(lines[start:end], page_metadata)
        for start, end in zip(item_indices, boundaries)
    ]


def build_article(markdown: str, page_metadata: dict[str, Any] | None = None) -> Article:
    return _build_article_from_lines(markdown.splitlines(), page_metadata)
