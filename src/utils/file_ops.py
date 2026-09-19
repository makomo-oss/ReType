"""Utility functions for file operations."""

import shutil
from pathlib import Path
import logging

logger = logging.getLogger(__name__)


def safe_delete(filepath: Path):
    """Safely delete a file, handling permission errors."""
    try:
        if filepath.exists():
            filepath.unlink()
            logger.info(f"Deleted: {filepath}")
            return True
    except PermissionError as e:
        logger.error(f"Permission denied deleting {filepath}: {e}")
    except Exception as e:
        logger.error(f"Failed to delete {filepath}: {e}")
    return False


def safe_rename(src: Path, dst: Path) -> bool:
    """Safely rename a file, handling conflicts."""
    try:
        if dst.exists():
            dst.unlink()
        src.rename(dst)
        logger.info(f"Renamed: {src.name} -> {dst.name}")
        return True
    except Exception as e:
        logger.error(f"Failed to rename {src.name}: {e}")
        return False


def safe_copy(src: Path, dst: Path) -> bool:
    """Safely copy a file."""
    try:
        shutil.copy2(src, dst)
        logger.info(f"Copied: {src.name} -> {dst.name}")
        return True
    except Exception as e:
        logger.error(f"Failed to copy {src.name}: {e}")
        return False


def generate_dst_path(src: Path, dst_ext: str, overwrite: bool = False) -> Path:
    """Generate destination file path.

    Args:
        src: Source file path
        dst_ext: Destination extension (with or without dot)
        overwrite: Whether to overwrite if destination exists

    Returns:
        Destination Path
    """
    dst_ext = dst_ext.lstrip('.')
    dst = src.parent / f"{src.stem}.{dst_ext}"

    if dst.exists() and not overwrite:
        counter = 1
        while True:
            dst = src.parent / f"{src.stem}_{counter}.{dst_ext}"
            if not dst.exists():
                break
            counter += 1
        logger.info(f"Destination exists, using: {dst.name}")

    return dst
