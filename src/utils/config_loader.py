"""Configuration loader for YAML settings."""

from pathlib import Path
from typing import Any
import yaml
import logging

logger = logging.getLogger(__name__)


class ConfigLoader:
    """Loads and manages application configuration."""

    DEFAULT_CONFIG = {
        'keep_original': True,
        'quality': 85,
        'overwrite_existing': False,
        'monitor': {
            'image': True,
            'video': True,
            'audio': True,
            'document': True,
        },
        'retry': {
            'max_attempts': 3,
            'delay_seconds': 1,
        },
        'image_formats': {},
        'video_formats': {},
        'audio_formats': {},
        'document_formats': {},
    }

    def __init__(self, config_path: str | Path):
        self.config_path = Path(config_path)
        self.config = self._load_config()

    def _load_config(self) -> dict[str, Any]:
        """Load configuration from YAML file, falling back to defaults."""
        if self.config_path.exists():
            try:
                with open(self.config_path, 'r', encoding='utf-8') as f:
                    loaded = yaml.safe_load(f) or {}
                # Merge with defaults
                merged = self.DEFAULT_CONFIG.copy()
                for key in merged:
                    if key in loaded:
                        if isinstance(merged[key], dict) and isinstance(loaded[key], dict):
                            merged[key] = {**merged[key], **loaded[key]}
                        else:
                            merged[key] = loaded[key]
                logger.info(f"Config loaded from: {self.config_path}")
                return merged
            except Exception as e:
                logger.warning(f"Failed to load config from {self.config_path}: {e}")
                return self.DEFAULT_CONFIG.copy()

        return self.DEFAULT_CONFIG.copy()

    @property
    def keep_original(self) -> bool:
        return self.config.get('keep_original', True)

    @property
    def quality(self) -> int:
        return self.config.get('quality', 85)

    @property
    def overwrite_existing(self) -> bool:
        return self.config.get('overwrite_existing', False)

    @property
    def monitor_settings(self) -> dict[str, bool]:
        return self.config.get('monitor', self.DEFAULT_CONFIG['monitor'])

    @property
    def retry_settings(self) -> dict[str, Any]:
        return self.config.get('retry', self.DEFAULT_CONFIG['retry'])

    def get_image_targets(self) -> dict[str, list[str]]:
        return self.config.get('image_formats', {})

    def get_video_targets(self) -> dict[str, list[str]]:
        return self.config.get('video_formats', {})

    def get_audio_targets(self) -> dict[str, list[str]]:
        return self.config.get('audio_formats', {})

    def get_document_targets(self) -> dict[str, list[str]]:
        return self.config.get('document_formats', {})
