"""File watcher using watchdog."""

import time
from pathlib import Path
from typing import Callable
import logging
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler

logger = logging.getLogger(__name__)


class FileChangeHandler(FileSystemEventHandler):
    """Handles file system events for the watched directory."""

    def __init__(self):
        super().__init__()
        self._events = []
        self._lock = False

    def on_modified(self, event):
        if not event.is_directory and event.src_path:
            self._add_event(Path(event.src_path), 'modified')

    def on_created(self, event):
        if not event.is_directory and event.src_path:
            self._add_event(Path(event.src_path), 'created')

    def on_moved(self, event):
        if not event.is_directory and event.dest_path:
            self._add_event(Path(event.dest_path), 'renamed')

    def _add_event(self, path: Path, event_type: str):
        """Add event to queue with debounce."""
        now = time.time()
        # Filter out temporary files
        if path.name.startswith('~$') or path.name.startswith('$'):
            return
        # Debounce: check if we already have an event for this file within 500ms
        for i, (prev_path, prev_time, prev_type) in enumerate(self._events):
            if prev_path == path and (now - prev_time) < 0.5:
                self._events[i] = (path, now, event_type)
                return
        self._events.append((path, now, event_type))

    def get_pending_events(self) -> list[tuple[Path, str]]:
        """Get all pending events and clear the queue."""
        events = list(self._events)
        self._events.clear()
        return events


class FileWatcher:
    """Manages file watching for directories."""

    def __init__(self):
        self._observer = Observer()
        self._handler = FileChangeHandler()
        self._watched_dirs = set()
        self._running = False

    def start_watching(self, directory: str | Path):
        """Start watching a directory for file changes."""
        directory = Path(directory).resolve()

        if not directory.exists():
            logger.error(f"Directory does not exist: {directory}")
            return

        if str(directory) in self._watched_dirs:
            logger.warning(f"Already watching: {directory}")
            return

        self._observer.schedule(self._handler, str(directory), recursive=False)
        self._watched_dirs.add(str(directory))
        logger.info(f"Started watching: {directory}")

        if not self._running:
            self._observer.start()
            self._running = True

    def stop_watching(self):
        """Stop all watching."""
        if self._running:
            self._observer.stop()
            self._running = False

    def join(self, timeout: float = None):
        """Wait for the observer to finish."""
        if self._running:
            if timeout:
                self._observer.join(timeout)
            else:
                while self._running:
                    time.sleep(0.1)

    def get_pending_events(self) -> list[tuple[Path, str]]:
        """Get pending file events."""
        return self._handler.get_pending_events()

    @property
    def is_running(self) -> bool:
        return self._running

    def stop(self):
        """Alias for stop_watching."""
        self.stop_watching()
