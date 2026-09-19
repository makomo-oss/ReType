"""Tests for video and audio converters."""

import pytest
from pathlib import Path
from unittest.mock import patch, MagicMock
from src.converters.video_conv import VideoConverter
from src.converters.audio_conv import AudioConverter


@pytest.fixture
def video_converter():
    return VideoConverter(quality=85)


@pytest.fixture
def audio_converter():
    return AudioConverter(quality=320)


class TestVideoConverter:
    """Test video format conversions."""

    @patch("src.converters.video_conv.subprocess.run")
    def test_convert_mp4_to_webm(self, mock_run, video_converter, tmp_path):
        src = tmp_path / "test.mp4"
        src.touch()
        dst = tmp_path / "test.webm"

        mock_run.return_value = MagicMock(returncode=0, stderr="")

        result = video_converter.convert(src, dst)
        assert result is True
        mock_run.assert_called_once()
        call_args = mock_run.call_args
        assert "-vcodec" in call_args[0][0]
        assert "libvpx-vp9" in call_args[0][0]

    @patch("src.converters.video_conv.subprocess.run")
    def test_convert_mp4_to_gif(self, mock_run, video_converter, tmp_path):
        src = tmp_path / "test.mp4"
        src.touch()
        dst = tmp_path / "test.gif"

        mock_run.return_value = MagicMock(returncode=0, stderr="")

        result = video_converter.convert(src, dst)
        assert result is True

    @patch("src.converters.video_conv.subprocess.run")
    def test_convert_failure(self, mock_run, video_converter, tmp_path):
        src = tmp_path / "test.mp4"
        src.touch()
        dst = tmp_path / "test.avi"

        mock_run.return_value = MagicMock(returncode=1, stderr="Error")

        result = video_converter.convert(src, dst)
        assert result is False
        assert not dst.exists()

    def test_get_supported_targets(self, video_converter):
        targets = video_converter.get_supported_targets("mp4")
        assert targets is not None
        assert "webm" in targets
        assert "avi" in targets
        assert "gif" in targets

        targets = video_converter.get_supported_targets("unknown")
        assert targets is None


class TestAudioConverter:
    """Test audio format conversions."""

    @patch("src.converters.audio_conv.subprocess.run")
    def test_convert_mp3_to_wav(self, mock_run, audio_converter, tmp_path):
        src = tmp_path / "test.mp3"
        src.touch()
        dst = tmp_path / "test.wav"

        mock_run.return_value = MagicMock(returncode=0, stderr="")

        result = audio_converter.convert(src, dst)
        assert result is True
        call_args = mock_run.call_args[0][0]
        assert "-acodec" in call_args
        assert "pcm_s16le" in call_args

    @patch("src.converters.audio_conv.subprocess.run")
    def test_convert_mp3_to_flac(self, mock_run, audio_converter, tmp_path):
        src = tmp_path / "test.mp3"
        src.touch()
        dst = tmp_path / "test.flac"

        mock_run.return_value = MagicMock(returncode=0, stderr="")

        result = audio_converter.convert(src, dst)
        assert result is True

    @patch("src.converters.audio_conv.subprocess.run")
    def test_convert_mp3_to_ogg(self, mock_run, audio_converter, tmp_path):
        src = tmp_path / "test.mp3"
        src.touch()
        dst = tmp_path / "test.ogg"

        mock_run.return_value = MagicMock(returncode=0, stderr="")

        result = audio_converter.convert(src, dst)
        assert result is True

    @patch("src.converters.audio_conv.subprocess.run")
    def test_convert_failure(self, mock_run, audio_converter, tmp_path):
        src = tmp_path / "test.mp3"
        src.touch()
        dst = tmp_path / "test.wav"

        mock_run.return_value = MagicMock(returncode=1, stderr="Error")

        result = audio_converter.convert(src, dst)
        assert result is False
        assert not dst.exists()

    def test_get_supported_targets(self, audio_converter):
        targets = audio_converter.get_supported_targets("mp3")
        assert targets is not None
        assert "wav" in targets
        assert "flac" in targets
        assert "ogg" in targets

        targets = audio_converter.get_supported_targets("unknown")
        assert targets is None
