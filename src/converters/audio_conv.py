"""Audio converter using FFmpeg."""

from pathlib import Path
from typing import Optional
import subprocess
import logging

from src.converters.base import BaseConverter

logger = logging.getLogger(__name__)

TARGET_MAPPINGS = {
    'mp3': ['wav', 'flac', 'ogg', 'm4a', 'aac'],
    'wav': ['mp3', 'flac', 'ogg', 'm4a'],
    'flac': ['mp3', 'wav', 'ogg', 'm4a'],
    'ogg': ['mp3', 'wav', 'flac', 'm4a'],
    'm4a': ['mp3', 'wav', 'flac', 'ogg'],
    'aac': ['mp3', 'wav', 'flac', 'ogg', 'm4a'],
}

AUDIO_CODECS = {
    'mp3': 'libmp3lame',
    'wav': 'pcm_s16le',
    'flac': 'flac',
    'ogg': 'libvorbis',
    'm4a': 'aac',
    'aac': 'aac',
}


class AudioConverter(BaseConverter):
    """Converts between audio formats using FFmpeg."""

    def __init__(self, quality: int = 85):
        self.quality = quality

    def convert(self, src: Path, dst: Path) -> bool:
        try:
            self._ensure_dst_directory(dst)

            dst_ext = dst.suffix.lower().lstrip('.')
            codec = AUDIO_CODECS.get(dst_ext, 'aac')

            cmd = [
                'ffmpeg',
                '-y',
                '-i', str(src),
                '-acodec', codec,
            ]

            # Quality settings per format
            if dst_ext == 'mp3':
                cmd.extend(['-ab', f'{self.quality}k'])
            elif dst_ext == 'flac':
                cmd.extend(['-compression_level', '6'])
            elif dst_ext == 'ogg':
                cmd.extend(['-q:a', str(max(0, min(10, self.quality // 10 - 2)))])
            elif dst_ext in ('m4a', 'aac'):
                cmd.extend(['-b:a', f'{self.quality}k'])

            cmd.extend([str(dst)])

            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=600,
            )

            if result.returncode != 0:
                logger.error(f"FFmpeg stderr: {result.stderr}")
                if dst.exists():
                    dst.unlink()
                return False

            logger.info(f"Audio converted: {src.name} -> {dst.name}")
            return True

        except subprocess.TimeoutExpired:
            logger.error(f"Audio conversion timeout: {src.name} -> {dst.name}")
            if dst.exists():
                dst.unlink()
            return False
        except Exception as e:
            logger.error(f"Audio conversion failed: {src.name} -> {dst.name}: {e}")
            if dst.exists():
                dst.unlink()
            return False

    def get_supported_targets(self, src_ext: str) -> Optional[list[str]]:
        src_ext = src_ext.lower().lstrip('.')
        return TARGET_MAPPINGS.get(src_ext)
