"""Image converter using Pillow."""

from pathlib import Path
from typing import Optional
import logging

from PIL import Image

from src.converters.base import BaseConverter

logger = logging.getLogger(__name__)

# Supported image formats by Pillow
SUPPORTED_EXTS = {
    'png': 'PNG',
    'jpeg': 'JPEG',
    'jpg': 'JPEG',
    'webp': 'WEBP',
    'bmp': 'BMP',
    'tiff': 'TIFF',
    'tif': 'TIFF',
    'gif': 'GIF',
    'ico': 'ICO',
    'pcx': 'PCX',
    'tga': 'TGA',
    'wmf': 'WMF',
    'eps': 'EPS',
    'epsf': 'EPS',
    'psd': 'PSD',
    'blp': 'BLP',
    'mpg': 'MPO',
    'sgi': 'SGI',
}

# Extension aliases
EXT_ALIASES = {
    'jpg': 'jpeg',
    'tif': 'tiff',
}

# Map internal format name to extension
FORMAT_TO_EXT = {v.lower(): k for k, v in SUPPORTED_EXTS.items()}

# Target extensions for each source extension
TARGET_MAPPINGS = {
    'png': ['jpeg', 'jpg', 'webp', 'bmp', 'tiff', 'gif', 'avif'],
    'jpeg': ['png', 'webp', 'bmp', 'tiff', 'gif', 'avif'],
    'jpg': ['png', 'webp', 'bmp', 'tiff', 'gif', 'avif'],
    'webp': ['png', 'jpeg', 'jpg', 'bmp', 'tiff', 'gif', 'avif'],
    'bmp': ['png', 'jpeg', 'jpg', 'webp', 'tiff', 'gif', 'avif'],
    'tiff': ['png', 'jpeg', 'jpg', 'webp', 'gif'],
    'tif': ['png', 'jpeg', 'jpg', 'webp', 'gif'],
    'gif': ['png', 'jpeg', 'jpg', 'webp', 'bmp', 'tiff'],
    'avif': ['png', 'jpeg', 'jpg', 'webp', 'bmp', 'tiff'],
}


def _normalize_ext(ext: str) -> str:
    """Normalize extension (jpg -> jpeg, tif -> tiff)."""
    return EXT_ALIASES.get(ext.lower(), ext.lower())


class ImageConverter(BaseConverter):
    """Converts between image formats using Pillow."""

    def __init__(self, quality: int = 85):
        self.quality = quality

    def convert(self, src: Path, dst: Path) -> bool:
        try:
            self._ensure_dst_directory(dst)

            with Image.open(src) as img:
                # Handle RGBA to RGB conversion for JPEG
                if dst.suffix.lower() in ('.jpeg', '.jpg') and img.mode in ('RGBA', 'LA', 'P'):
                    # Create white background
                    background = Image.new('RGB', img.size, (255, 255, 255))
                    if img.mode == 'P':
                        img = img.convert('RGBA')
                    if img.mode in ('RGBA', 'LA'):
                        background.paste(img, mask=img.split()[-1])
                        img = background
                    else:
                        img = img.convert('RGB')
                elif dst.suffix.lower() == '.webp' and img.mode == 'P':
                    img = img.convert('RGBA')
                elif dst.suffix.lower() == '.avif' and img.mode in ('PA', 'LA'):
                    img = img.convert('RGBA')

                # Determine save format and parameters
                dst_ext = dst.suffix.lower().lstrip('.')

                save_kwargs = {}
                if dst_ext in ('jpeg', 'jpg'):
                    save_kwargs['quality'] = self.quality
                    save_kwargs['optimize'] = True
                    save_kwargs['format'] = 'JPEG'
                elif dst_ext == 'png':
                    save_kwargs['optimize'] = True
                    save_kwargs['format'] = 'PNG'
                elif dst_ext == 'webp':
                    save_kwargs['quality'] = self.quality
                    save_kwargs['optimize'] = True
                    save_kwargs['format'] = 'WEBP'
                    if img.mode == 'RGBA':
                        save_kwargs['lossless'] = True
                elif dst_ext == 'bmp':
                    save_kwargs['format'] = 'BMP'
                elif dst_ext in ('tiff', 'tif'):
                    save_kwargs['optimize'] = True
                    save_kwargs['format'] = 'TIFF'
                elif dst_ext == 'gif':
                    if img.mode != 'P':
                        img = img.convert('P', palette=Image.Palette.ADAPTIVE)
                    save_kwargs['format'] = 'GIF'
                elif dst_ext == 'avif':
                    try:
                        save_kwargs['format'] = 'AVIF'
                    except KeyError:
                        logger.warning("AVIF format not supported by this Pillow version")
                        return False
                elif dst_ext == 'ico':
                    save_kwargs['format'] = 'ICO'
                elif dst_ext == 'pcx':
                    save_kwargs['format'] = 'PCX'
                elif dst_ext == 'tga':
                    save_kwargs['format'] = 'TGA'
                elif dst_ext == 'wmf':
                    save_kwargs['format'] = 'WMF'
                elif dst_ext == 'epsf':
                    save_kwargs['format'] = 'EPS'
                elif dst_ext == 'psd':
                    save_kwargs['format'] = 'PSD'
                elif dst_ext == 'blp':
                    save_kwargs['format'] = 'BLP'
                elif dst_ext == 'sgi':
                    save_kwargs['format'] = 'SGI'
                else:
                    logger.error(f"Unknown image format: {dst_ext}")
                    return False

                img.save(dst, **save_kwargs)

            logger.info(f"Image converted: {src.name} -> {dst.name}")
            return True

        except Exception as e:
            logger.error(f"Image conversion failed: {src.name} -> {dst.name}: {e}")
            if dst.exists():
                dst.unlink()
            return False

    def get_supported_targets(self, src_ext: str) -> Optional[list[str]]:
        src_ext = _normalize_ext(src_ext)
        return TARGET_MAPPINGS.get(src_ext)
