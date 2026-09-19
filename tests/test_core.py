"""Tests for core modules."""

import pytest
from pathlib import Path
from src.core.detector import ExtensionDetector
from src.core.watcher import FileChangeHandler
from src.utils.config_loader import ConfigLoader
from src.utils.file_ops import safe_delete, generate_dst_path


class TestExtensionDetector:
    """Test extension detection logic."""

    def test_detect_change(self, tmp_path):
        file_path = tmp_path / "test.png"
        file_path.touch()
        detector = ExtensionDetector(file_path)

        # No change needed
        assert detector.detect_change("png") is None

        # Change detected
        assert detector.detect_change("jpeg") == "jpeg"

    def test_detect_no_extension(self, tmp_path):
        file_path = tmp_path / "test"
        file_path.touch()
        detector = ExtensionDetector(file_path)
        assert detector.original_ext == ""

    def test_validate_source_exists(self, tmp_path):
        file_path = tmp_path / "test.png"
        file_path.touch()
        detector = ExtensionDetector(file_path)
        assert detector.validate_source() is True

    def test_validate_source_not_exists(self, tmp_path):
        file_path = tmp_path / "nonexistent.png"
        detector = ExtensionDetector(file_path)
        assert detector.validate_source() is False

    def test_current_ext_after_rename(self, tmp_path):
        file_path = tmp_path / "test.png"
        file_path.touch()
        detector = ExtensionDetector(file_path)

        # Before rename, should return the path's extension
        assert detector.current_ext == "png"

        # After rename, file at original path no longer exists
        # Should fall back to original_ext
        file_path.rename(tmp_path / "test.jpeg")
        assert detector.current_ext == detector.original_ext


class TestConfigLoader:
    """Test configuration loading."""

    def test_load_default_config(self, tmp_path):
        config_path = tmp_path / "settings.yaml"
        loader = ConfigLoader(config_path)

        assert loader.keep_original is True
        assert loader.quality == 85
        assert loader.overwrite_existing is False

    def test_load_config_from_file(self, tmp_path):
        config_content = """
keep_original: false
quality: 95
overwrite_existing: true
"""
        config_path = tmp_path / "settings.yaml"
        config_path.write_text(config_content)

        loader = ConfigLoader(config_path)
        assert loader.keep_original is False
        assert loader.quality == 95
        assert loader.overwrite_existing is True

    def test_load_config_with_image_formats(self, tmp_path):
        config_content = """
image_formats:
  png:
    - jpeg
    - webp
  jpeg:
    - png
"""
        config_path = tmp_path / "settings.yaml"
        config_path.write_text(config_content)

        loader = ConfigLoader(config_path)
        targets = loader.get_image_targets()
        assert "png" in targets
        assert "jpeg" in targets["png"]


class TestFileOps:
    """Test file operation utilities."""

    def test_safe_delete(self, tmp_path):
        file_path = tmp_path / "test.txt"
        file_path.touch()
        assert safe_delete(file_path) is True
        assert not file_path.exists()

    def test_safe_delete_nonexistent(self, tmp_path):
        file_path = tmp_path / "nonexistent.txt"
        assert safe_delete(file_path) is False

    def test_generate_dst_path(self, tmp_path):
        src = tmp_path / "test.png"
        src.touch()
        dst = generate_dst_path(src, "jpeg")
        assert str(dst) == str(tmp_path / "test.jpeg")

    def test_generate_dst_path_with_suffix_dot(self, tmp_path):
        src = tmp_path / "test.png"
        src.touch()
        dst = generate_dst_path(src, ".jpg")
        assert str(dst) == str(tmp_path / "test.jpg")

    def test_generate_dst_path_avoid_conflict(self, tmp_path):
        src = tmp_path / "test.png"
        src.touch()
        existing = tmp_path / "test.jpeg"
        existing.touch()
        dst = generate_dst_path(src, "jpeg")
        assert str(dst) == str(tmp_path / "test_1.jpeg")
