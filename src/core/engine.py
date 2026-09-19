"""Conversion engine that orchestrates the conversion process."""

from pathlib import Path
from typing import Optional
import threading
import time
import logging

from src.converters.image_conv import ImageConverter
from src.converters.video_conv import VideoConverter
from src.converters.audio_conv import AudioConverter
from src.converters.doc_conv import DocumentConverter
from src.converters.base import BaseConverter
from src.core.detector import ExtensionDetector
from src.core.watcher import FileWatcher
from src.utils.config_loader import ConfigLoader
from src.utils.file_ops import safe_delete, generate_dst_path

logger = logging.getLogger(__name__)


class ConversionEngine:
    """Orchestrates the file conversion process."""

    def __init__(self, config: ConfigLoader):
        self.config = config
        self._converters: dict[str, BaseConverter] = {}
        self._ext_to_type: dict[str, str] = {}
        self._init_converters()
        self._watcher: Optional[FileWatcher] = None
        self._watching = False
        self._convert_dir: Optional[Path] = None

        # Conversion tracking
        self._converting_files: set[str] = set()

    def _init_converters(self):
        """Initialize all available converters."""
        quality = self.config.quality

        # Initialize converters
        converters = [
            ('image', ImageConverter(quality)),
            ('video', VideoConverter(quality)),
            ('audio', AudioConverter(quality)),
            ('document', DocumentConverter()),
        ]

        # Build extension to type mapping and converter registry
        for name, converter in converters:
            self._converters[name] = converter
            # Get supported extensions for this converter type
            targets = self._get_targets_for_type(name)
            # We need to reverse the mapping: target extensions -> source type
            for src_ext in self._get_all_source_exts(name):
                if self.config.monitor_settings.get(name, False):
                    self._ext_to_type[src_ext] = name

    def _get_all_source_exts(self, converter_type: str) -> list[str]:
        """Get all source extensions for a converter type."""
        targets = self._get_targets_for_type(converter_type)
        exts = []
        for src_ext, targets_list in targets.items():
            exts.extend(src_ext.split(','))
        return exts

    def _get_targets_for_type(self, converter_type: str) -> dict:
        """Get target mappings for a converter type."""
        if converter_type == 'image':
            return self.config.get_image_targets()
        elif converter_type == 'video':
            return self.config.get_video_targets()
        elif converter_type == 'audio':
            return self.config.get_audio_targets()
        elif converter_type == 'document':
            return self.config.get_document_targets()
        return {}

    def convert(self, file_path: Path) -> bool:
        """Convert a file based on its extension change.

        Args:
            file_path: Path to the file that was modified/renamed

        Returns:
            True if conversion was successful, False otherwise
        """
        file_key = str(file_path.resolve())

        # Prevent concurrent conversions of the same file
        if file_key in self._converting_files:
            logger.debug(f"Already converting: {file_path.name}")
            return False
        self._converting_files.add(file_key)

        try:
            detector = ExtensionDetector(file_path)

            # Validate source file
            if not detector.validate_source():
                return False

            # Get current file extension
            current_ext = detector.current_ext

            # Skip if it's a temp file
            if current_ext.startswith('.~') or current_ext.startswith('$'):
                logger.debug(f"Skipping temp file: {file_path.name}")
                return False

            # Find the converter for this file type
            converter_type = self._ext_to_type.get(current_ext.lower())
            if not converter_type:
                logger.debug(f"No converter for extension: {current_ext}")
                return False

            # Check if monitoring is enabled for this type
            if not self.config.monitor_settings.get(converter_type, False):
                logger.debug(f"Monitoring disabled for: {converter_type}")
                return False

            converter = self._converters[converter_type]

            # Get supported targets for this file type
            targets = converter.get_supported_targets(current_ext)
            if not targets:
                logger.debug(f"No targets for: {current_ext}")
                return False

            logger.info(f"Detected conversion: {file_path.name} ({current_ext} -> any)")

            # Since we're watching for any modification, we need to check
            # if the file's new extension matches a supported target
            new_ext = detector.current_ext
            if new_ext in targets:
                return self._do_convert(file_path, converter, new_ext, current_ext)
            else:
                # Check if the new extension is in any of the target lists
                for target in targets:
                    if new_ext == target:
                        return self._do_convert(file_path, converter, target, current_ext)

                # File might still be being written - check if it's a new target
                all_targets = set()
                for t in targets:
                    all_targets.add(t)
                if new_ext not in all_targets:
                    logger.debug(f"Extension {new_ext} not in targets for {current_ext}")
                    return False

                return self._do_convert(file_path, converter, new_ext, current_ext)

        finally:
            self._converting_files.discard(file_key)

    def _do_convert(self, file_path: Path, converter: BaseConverter, dst_ext: str, src_ext: str) -> bool:
        """Execute the actual conversion."""
        try:
            dst_path = generate_dst_path(
                file_path,
                dst_ext,
                self.config.overwrite_existing,
            )

            logger.info(f"Converting: {file_path.name} ({src_ext}) -> {dst_path.name} ({dst_ext})")

            success = converter.convert(file_path, dst_path)

            if success:
                # Handle original file
                if not self.config.keep_original:
                    safe_delete(file_path)
                else:
                    logger.debug(f"Keeping original: {file_path.name}")

                return True
            else:
                logger.error(f"Conversion failed: {file_path.name}")
                return False

        except Exception as e:
            logger.error(f"Unexpected error during conversion: {e}")
            return False

    def start_watch(self, directory: str | Path):
        """Start watching a directory for file changes."""
        self._convert_dir = Path(directory).resolve()
        self._watcher = FileWatcher()
        self._watcher.start_watching(self._convert_dir)
        self._watching = True
        logger.info(f"Started watching directory: {self._convert_dir}")

    def process_events(self) -> int:
        """Process pending file events.

        Returns:
            Number of files converted
        """
        if not self._watching or not self._watcher:
            return 0

        events = self._watcher.get_pending_events()
        converted = 0

        for file_path, event_type in events:
            # Only process files in the watched directory
            try:
                file_path.resolve().parent == self._convert_dir.resolve()
            except (ValueError, OSError):
                continue

            if event_type == 'renamed':
                logger.info(f"File renamed event: {file_path.name}")
                converted += 1
                self.convert(file_path)
            elif event_type == 'modified':
                logger.info(f"File modified event: {file_path.name}")
                converted += 1
                self.convert(file_path)

        return converted

    def stop_watch(self):
        """Stop watching for file changes."""
        if self._watcher:
            self._watcher.stop_watching()
            self._watching = False
            logger.info("Stopped watching directory")

    @property
    def is_watching(self) -> bool:
        return self._watching
