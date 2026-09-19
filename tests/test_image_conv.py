"""Tests for image converter."""

import pytest
from pathlib import Path
from PIL import Image
from src.converters.image_conv import ImageConverter


@pytest.fixture
def converter():
    return ImageConverter(quality=90)


@pytest.fixture
def temp_dir(tmp_path):
    return tmp_path / "test_images"


@pytest.fixture
def test_png_path(temp_dir):
    """Create a test PNG file."""
    temp_dir.mkdir(parents=True, exist_ok=True)
    img = Image.new("RGBA", (100, 100), (255, 0, 0, 255))
    path = temp_dir / "test.png"
    img.save(path, "PNG")
    return path


@pytest.fixture
def test_jpg_path(temp_dir):
    """Create a test JPG file."""
    temp_dir.mkdir(parents=True, exist_ok=True)
    img = Image.new("RGB", (100, 100), (0, 255, 0))
    path = temp_dir / "test.jpg"
    img.save(path, "JPEG")
    return path


@pytest.fixture
def test_webp_path(temp_dir):
    """Create a test WebP file."""
    temp_dir.mkdir(parents=True, exist_ok=True)
    img = Image.new("RGB", (100, 100), (0, 0, 255))
    path = temp_dir / "test.webp"
    img.save(path, "WEBP")
    return path


class TestImageConverter:
    """Test image format conversions."""

    def test_convert_png_to_jpeg(self, converter, test_png_path, temp_dir):
        dst = temp_dir / "test.jpeg"
        result = converter.convert(test_png_path, dst)
        assert result is True
        assert dst.exists()
        assert dst.stat().st_size > 0

        # Verify it's a valid JPEG
        with Image.open(dst) as img:
            assert img.format == "JPEG"
            assert img.mode == "RGB"

    def test_convert_png_to_webp(self, converter, test_png_path, temp_dir):
        dst = temp_dir / "test.webp"
        result = converter.convert(test_png_path, dst)
        assert result is True
        assert dst.exists()

        with Image.open(dst) as img:
            assert img.format == "WEBP"

    def test_convert_png_to_bmp(self, converter, test_png_path, temp_dir):
        dst = temp_dir / "test.bmp"
        result = converter.convert(test_png_path, dst)
        assert result is True
        assert dst.exists()

        with Image.open(dst) as img:
            assert img.format == "BMP"

    def test_convert_png_to_tiff(self, converter, test_png_path, temp_dir):
        dst = temp_dir / "test.tiff"
        result = converter.convert(test_png_path, dst)
        assert result is True
        assert dst.exists()

        with Image.open(dst) as img:
            assert img.format == "TIFF"

    def test_convert_png_to_gif(self, converter, test_png_path, temp_dir):
        dst = temp_dir / "test.gif"
        result = converter.convert(test_png_path, dst)
        assert result is True
        assert dst.exists()

        with Image.open(dst) as img:
            assert img.format == "GIF"

    def test_convert_jpg_to_png(self, converter, test_jpg_path, temp_dir):
        dst = temp_dir / "test.png"
        result = converter.convert(test_jpg_path, dst)
        assert result is True
        assert dst.exists()

        with Image.open(dst) as img:
            assert img.format == "PNG"

    def test_convert_jpg_to_webp(self, converter, test_jpg_path, temp_dir):
        dst = temp_dir / "test.webp"
        result = converter.convert(test_jpg_path, dst)
        assert result is True
        assert dst.exists()

    def test_convert_webp_to_png(self, converter, test_webp_path, temp_dir):
        dst = temp_dir / "test.png"
        result = converter.convert(test_webp_path, dst)
        assert result is True
        assert dst.exists()

        with Image.open(dst) as img:
            assert img.format == "PNG"

    def test_convert_webp_to_jpeg(self, converter, test_webp_path, temp_dir):
        dst = temp_dir / "test.jpeg"
        result = converter.convert(test_webp_path, dst)
        assert result is True
        assert dst.exists()

    def test_get_supported_targets(self, converter):
        targets = converter.get_supported_targets("png")
        assert targets is not None
        assert "jpeg" in targets
        assert "webp" in targets

        targets = converter.get_supported_targets("jpeg")
        assert targets is not None
        assert "png" in targets

        targets = converter.get_supported_targets("unknown")
        assert targets is None

    def test_convert_to_same_format(self, converter, test_png_path, temp_dir):
        """Converting to the same format should still work."""
        dst = temp_dir / "test_copy.png"
        result = converter.convert(test_png_path, dst)
        assert result is True
        assert dst.exists()

    def test_convert_large_image(self, converter, temp_dir):
        """Test conversion of a larger image."""
        temp_dir.mkdir(parents=True, exist_ok=True)
        img = Image.new("RGBA", (1920, 1080), (128, 128, 128, 255))
        src = temp_dir / "large.png"
        img.save(src, "PNG")

        dst = temp_dir / "large.jpeg"
        result = converter.convert(src, dst)
        assert result is True
        assert dst.exists()

        with Image.open(dst) as img:
            assert img.size == (1920, 1080)

    def test_fail_on_invalid_source(self, converter, temp_dir):
        """Test handling of non-existent source file."""
        dst = temp_dir / "output.jpeg"
        result = converter.convert(Path("/nonexistent/file.png"), dst)
        assert result is False

    def test_rgba_to_rgb_conversion(self, converter, temp_dir):
        """Test that RGBA images are properly converted to RGB for JPEG."""
        temp_dir.mkdir(parents=True, exist_ok=True)
        img = Image.new("RGBA", (100, 100), (255, 0, 0, 128))
        src = temp_dir / "rgba.png"
        img.save(src, "PNG")

        dst = temp_dir / "rgba.jpeg"
        result = converter.convert(src, dst)
        assert result is True
        assert dst.exists()

        with Image.open(dst) as img:
            assert img.mode == "RGB"

    def test_convert_with_different_quality(self):
        """Test that quality parameter affects output."""
        converter_high = ImageConverter(quality=95)
        converter_low = ImageConverter(quality=50)

        assert converter_high.quality == 95
        assert converter_low.quality == 50
