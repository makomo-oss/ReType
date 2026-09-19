"""Base converter class for all format converters."""

from abc import ABC, abstractmethod
from pathlib import Path
from typing import Optional
import logging

logger = logging.getLogger(__name__)


class BaseConverter(ABC):
    """Abstract base class for file converters."""

    @abstractmethod
    def convert(self, src: Path, dst: Path) -> bool:
        """Convert file from src to dst. Returns True on success."""
        ...

    @abstractmethod
    def get_supported_targets(self, src_ext: str) -> Optional[list[str]]:
        """Get list of supported target extensions for a given source extension."""
        ...

    @staticmethod
    def _get_filename_without_ext(path: Path) -> str:
        """Get filename without extension."""
        return path.stem

    @staticmethod
    def _ensure_dst_directory(dst: Path):
        """Ensure the directory for dst exists."""
        dst.parent.mkdir(parents=True, exist_ok=True)

    @staticmethod
    def _is_compatible(src: Path, dst: Path) -> bool:
        """Check if source and destination are compatible file types."""
        return src.suffix.lower().lstrip('.') != dst.suffix.lower().lstrip('.')
