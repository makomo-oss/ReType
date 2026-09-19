"""Document converter for text/markdown/html/pdf formats."""

from pathlib import Path
from typing import Optional
import subprocess
import logging
import shutil

from src.converters.base import BaseConverter

logger = logging.getLogger(__name__)

TARGET_MAPPINGS = {
    'md': ['txt', 'html', 'pdf'],
    'txt': ['md', 'html', 'pdf'],
    'html': ['md', 'txt', 'pdf'],
    'htm': ['md', 'txt', 'pdf'],
    'rtf': ['md', 'txt', 'html', 'pdf'],
}


class DocumentConverter(BaseConverter):
    """Converts between document formats using pandoc where available."""

    def convert(self, src: Path, dst: Path) -> bool:
        try:
            self._ensure_dst_directory(dst)

            src_ext = src.suffix.lower().lstrip('.')
            dst_ext = dst.suffix.lower().lstrip('.')

            # Try pandoc first for text-based formats
            if self._has_pandoc():
                return self._convert_with_pandoc(src, dst, src_ext, dst_ext)

            # Fallback for simple text-based conversions
            return self._convert_text_fallback(src, dst, src_ext, dst_ext)

        except Exception as e:
            logger.error(f"Document conversion failed: {src.name} -> {dst.name}: {e}")
            if dst.exists():
                dst.unlink()
            return False

    def _has_pandoc(self) -> bool:
        """Check if pandoc is available."""
        return shutil.which('pandoc') is not None

    def _convert_with_pandoc(self, src: Path, dst: Path, src_ext: str, dst_ext: str) -> bool:
        """Convert using pandoc."""
        try:
            cmd = ['pandoc']

            if src_ext == 'rtf':
                cmd.extend(['-f', 'rtf'])
            elif src_ext == 'md':
                cmd.extend(['-f', 'markdown'])
            elif src_ext == 'html' or src_ext == 'htm':
                cmd.extend(['-f', 'html'])
            elif src_ext == 'txt':
                cmd.extend(['-f', 'plain'])

            cmd.extend(['-o', str(dst)])

            if dst_ext == 'pdf':
                cmd.extend(['-t', 'latex'])
                cmd.extend(['-V', 'documentclass=article'])
            elif dst_ext == 'html' or dst_ext == 'htm':
                cmd.extend(['-t', 'html'])
            elif dst_ext == 'md':
                cmd.extend(['-t', 'markdown'])
            elif dst_ext == 'txt':
                cmd.extend(['-t', 'plain'])

            cmd.append(str(src))

            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=60,
            )

            if result.returncode != 0:
                logger.warning(f"pandoc stderr: {result.stderr}")
                return self._convert_text_fallback(src, dst, src_ext, dst_ext)

            logger.info(f"Document converted (pandoc): {src.name} -> {dst.name}")
            return True

        except (subprocess.TimeoutExpired, FileNotFoundError):
            return self._convert_text_fallback(src, dst, src_ext, dst_ext)

    def _convert_text_fallback(self, src: Path, dst: Path, src_ext: str, dst_ext: str) -> bool:
        """Fallback for text-based conversions without pandoc."""
        text_exts = {'txt', 'md', 'html', 'htm', 'rtf'}
        target_exts = {'txt', 'md', 'html', 'htm'}

        # Only safe for text-based formats
        if src_ext not in text_exts or dst_ext not in target_exts:
            return False

        try:
            # Simple content copy for compatible formats
            content = src.read_text(encoding='utf-8')
            dst.write_text(content, encoding='utf-8')
            logger.info(f"Document converted (fallback): {src.name} -> {dst.name}")
            return True
        except Exception as e:
            logger.error(f"Fallback conversion failed: {e}")
            return False

    def get_supported_targets(self, src_ext: str) -> Optional[list[str]]:
        src_ext = src_ext.lower().lstrip('.')
        return TARGET_MAPPINGS.get(src_ext)
