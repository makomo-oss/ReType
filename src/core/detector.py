"""Extension change detector."""

from pathlib import Path
from typing import Optional
import logging

logger = logging.getLogger(__name__)


class ExtensionDetector:
    """Detects file type changes and validates conversions."""

    def __init__(self, original_path: Path):
        self.original_path = original_path
        self.original_ext = original_path.suffix.lower().lstrip('.')
        self.original_name = original_path.stem

    @property
    def current_ext(self) -> str:
        """Get the current file extension."""
        if self.original_path.exists():
            return self.original_path.suffix.lower().lstrip('.')
        return self.original_ext

    def detect_change(self, target_ext: str) -> Optional[str]:
        """Detect if a conversion from current ext to target_ext is needed.

        Returns:
            None if no conversion needed, target_ext if conversion is needed.
        """
        current = self.current_ext
        if current == target_ext:
            return None
        return target_ext

    def validate_source(self) -> bool:
        """Validate that the source file exists and is readable."""
        if not self.original_path.exists():
            logger.error(f"Source file does not exist: {self.original_path}")
            return False

        try:
            self.original_path.stat()
            return True
        except Exception as e:
            logger.error(f"Cannot read source file: {e}")
            return False

    def __repr__(self):
        return f"ExtensionDetector(src={self.original_path.name}, original_ext={self.original_ext})"
