"""Logging configuration for ReType."""

import logging
import sys
from pathlib import Path
from loguru import logger as loguru_logger
from loguru._defaults import LOGGER_MIN_LEVEL


def setup_logging(log_dir: str | Path = None, level: str = "DEBUG"):
    """Configure application logging.

    Args:
        log_dir: Directory for log files. Defaults to ~/.local/share/retype/logs
        level: Logging level string
    """
    # Remove default handler
    loguru_logger.remove()

    # Add console handler
    loguru_logger.add(
        sys.stderr,
        level=level,
        format="<green>{time:YYYY-MM-DD HH:mm:ss}</green> | <level>{level: <8}</level> | <cyan>{name}</cyan>:<cyan>{function}</cyan>:<cyan>{line}</cyan> - <level>{message}</level>",
    )

    # Add file handler
    if log_dir:
        log_dir = Path(log_dir)
        log_dir.mkdir(parents=True, exist_ok=True)
        log_file = log_dir / "retype.log"
        loguru_logger.add(
            log_file,
            level=level,
            rotation="10 MB",
            retention="30 days",
            encoding="utf-8",
            format="{time:YYYY-MM-DD HH:mm:ss} | {level: <8} | {name}:{function}:{line} - {message}",
        )
        loguru_logger.info(f"Logging to: {log_file}")

    return loguru_logger
