"""Video converter using FFmpeg."""

from pathlib import Path
from typing import Optional
import subprocess
import logging

from src.converters.base import BaseConverter

logger = logging.getLogger(__name__)

TARGET_MAPPINGS = {
    'mp4': ['webm', 'avi', 'mov', 'mkv', 'gif'],
    'webm': ['mp4', 'avi', 'mov', 'mkv', 'gif'],
    'avi': ['mp4', 'webm', 'mov', 'mkv'],
    'mov': ['mp4', 'webm', 'avi', 'mkv'],
    'mkv': ['mp4', 'webm', 'avi', 'mov', 'gif'],
    'gif': ['mp4', 'webm', 'avi', 'mov', 'mkv'],
}

# Codec mappings for different output formats
VIDEO_CODEC_MAP = {
    'mp4': ('libx264', 'aac'),
    'webm': ('libvpx-vp9', 'libvorbis'),
    'avi': ('mpeg4', 'mp3'),
    'mov': ('libx264', 'aac'),
    'mkv': ('libx264', 'aac'),
    'gif': (None, None),  # Special handling
}


class VideoConverter(BaseConverter):
    """Converts between video formats using FFmpeg."""

    def __init__(self, quality: int = 85):
        self.quality = quality

    def convert(self, src: Path, dst: Path) -> bool:
        try:
            self._ensure_dst_directory(dst)

            dst_ext = dst.suffix.lower().lstrip('.')

            if dst_ext == 'gif':
                return self._convert_to_gif(src, dst)

            cmd = [
                'ffmpeg',
                '-y',
                '-i', str(src),
            ]

            codec_name, audio_codec = VIDEO_CODEC_MAP.get(dst_ext, ('libx264', 'aac'))
            if codec_name:
                cmd.extend(['-vcodec', codec_name])
            if audio_codec:
                cmd.extend(['-acodec', audio_codec])

            if dst_ext == 'webm':
                cmd.extend(['-crf', '23'])
            elif dst_ext == 'mp4':
                cmd.extend(['-crf', str(max(18, min(28, 28 - int(self.quality / 10)))]))
            elif dst_ext == 'mkv':
                cmd.extend(['-crf', '23'])

            cmd.extend(['-pix_fmt', 'yuv420p'])
            cmd.extend([str(dst)])

            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=3600,
            )

            if result.returncode != 0:
                logger.error(f"FFmpeg stderr: {result.stderr}")
                if dst.exists():
                    dst.unlink()
                return False

            logger.info(f"Video converted: {src.name} -> {dst.name}")
            return True

        except subprocess.TimeoutExpired:
            logger.error(f"Video conversion timeout: {src.name} -> {dst.name}")
            if dst.exists():
                dst.unlink()
            return False
        except Exception as e:
            logger.error(f"Video conversion failed: {src.name} -> {dst.name}: {e}")
            if dst.exists():
                dst.unlink()
            return False

    def _convert_to_gif(self, src: Path, dst: Path) -> bool:
        """Convert video to GIF animation."""
        try:
            self._ensure_dst_directory(dst)

            cmd = [
                'ffmpeg',
                '-y',
                '-i', str(src),
                '-vf', 'fps=10,scale=640:-1:flags=lanczos,split[s0][s1];[s0]palettegen=pal=dither=bayer:bayer_scale=5:max_colors=256[s1];[s1][s2]paletteuse=dither=bayer:bayer_scale=5:max_colors=256',
                '-loop', '0',
                str(dst),
            ]

            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=600,
            )

            if result.returncode != 0:
                logger.error(f"GIF conversion stderr: {result.stderr}")
                if dst.exists():
                    dst.unlink()
                return False

            logger.info(f"Video converted to GIF: {src.name} -> {dst.name}")
            return True

        except Exception as e:
            logger.error(f"GIF conversion failed: {src.name} -> {dst.name}: {e}")
            if dst.exists():
                dst.unlink()
            return False

    def get_supported_targets(self, src_ext: str) -> Optional[list[str]]:
        src_ext = src_ext.lower().lstrip('.')
        return TARGET_MAPPINGS.get(src_ext)
