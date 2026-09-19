"""Main window UI for ReType."""

import sys
import time
from pathlib import Path
from typing import Optional

from PyQt6.QtWidgets import (
    QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, QPushButton,
    QLabel, QListWidget, QListWidgetItem, QMessageBox,
    QGroupBox, QStatusBar, QToolBar, QAction, QApplication,
)
from PyQt6.QtCore import Qt, QThread, pyqtSignal
from PyQt6.QtGui import QIcon

from src.ui.components import DropZoneWidget, ResultsListWidget
from src.core.engine import ConversionEngine
from src.utils.config_loader import ConfigLoader
from src.utils.logger import setup_logging


class ConversionWorker(QThread):
    """Worker thread for processing conversions."""

    result_signal = pyqtSignal(str, str, str)  # filename, status, message
    finished_signal = pyqtSignal()

    def __init__(self, engine: ConversionEngine, files: list[str]):
        super().__init__()
        self.engine = engine
        self.files = files

    def run(self):
        for filepath in self.files:
            file_path = Path(filepath)
            try:
                success = self.engine.convert(file_path)
                if success:
                    self.result_signal.emit(
                        file_path.name,
                        "success",
                        f"Converted successfully"
                    )
                else:
                    self.result_signal.emit(
                        file_path.name,
                        "error",
                        "Conversion failed"
                    )
            except Exception as e:
                self.result_signal.emit(
                    file_path.name,
                    "error",
                    str(e)
                )
        self.finished_signal.emit()


class MainWindow(QMainWindow):
    """Main application window."""

    def __init__(self, config: ConfigLoader):
        super().__init__()
        self.config = config
        self.engine = ConversionEngine(config)
        self.worker: Optional[ConversionWorker] = None
        self.watch_dir: Optional[Path] = None
        self.watch_thread: Optional[QThread] = None

        self.init_ui()
        self.setup_conversion_watch()

    def init_ui(self):
        """Initialize the user interface."""
        self.setWindowTitle("ReType - File Extension Converter")
        self.setMinimumSize(700, 600)
        self.setMaximumSize(900, 700)

        # Central widget
        central = QWidget()
        self.setCentralWidget(central)
        layout = QVBoxLayout(central)
        layout.setSpacing(15)

        # Title
        title_label = QLabel("ReType")
        title_label.setStyleSheet("font-size: 24px; font-weight: bold; color: #0078d4;")
        title_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(title_label)

        # Subtitle
        subtitle = QLabel("Drop files here or add a watch directory to start converting")
        subtitle.setAlignment(Qt.AlignmentFlag.AlignCenter)
        subtitle.setStyleSheet("color: #888888; font-size: 12px;")
        layout.addWidget(subtitle)

        # Drop zone
        self.drop_zone = DropZoneWidget()
        self.drop_zone.files_dropped.connect(self.on_files_dropped)
        layout.addWidget(self.drop_zone)

        # Button group
        btn_layout = QHBoxLayout()

        add_btn = QPushButton("Add Watch Directory")
        add_btn.setStyleSheet("""
            QPushButton {
                background-color: #0078d4;
                color: white;
                border: none;
                border-radius: 5px;
                padding: 10px 20px;
                font-size: 13px;
            }
            QPushButton:hover {
                background-color: #106ebe;
            }
        """)
        add_btn.clicked.connect(self.add_watch_directory)
        btn_layout.addWidget(add_btn)

        clear_btn = QPushButton("Clear Results")
        clear_btn.setStyleSheet("""
            QPushButton {
                background-color: #3e3e42;
                color: white;
                border: none;
                border-radius: 5px;
                padding: 10px 20px;
                font-size: 13px;
            }
            QPushButton:hover {
                background-color: #4e4e52;
            }
        """)
        clear_btn.clicked.connect(self.clear_results)
        btn_layout.addWidget(clear_btn)

        layout.addLayout(btn_layout)

        # Results group
        results_group = QGroupBox("Conversion Results")
        results_group.setStyleSheet("""
            QGroupBox {
                font-weight: bold;
                font-size: 14px;
                border: 1px solid #333333;
                border-radius: 5px;
                margin-top: 10px;
                padding-top: 10px;
            }
            QGroupBox::title {
                subcontrol-origin: margin;
                left: 10px;
                padding: 0 5px;
            }
        """)
        results_layout = QVBoxLayout(results_group)

        self.results_list = ResultsListWidget()
        results_layout.addWidget(self.results_list)

        layout.addWidget(results_group)

        # Status bar
        self.status_bar = QStatusBar()
        self.setStatusBar(self.status_bar)
        self.status_label = QLabel("Ready")
        self.status_bar.addWidget(self.status_label)
        self.status_bar.showMessage("Ready - Drop files or add a watch directory")

        # Settings info
        info_layout = QHBoxLayout()
        info_label = QLabel(f"Quality: {self.config.quality} | Keep Original: {self.config.keep_original}")
        info_label.setStyleSheet("color: #666666; font-size: 11px;")
        info_layout.addWidget(info_label)
        info_layout.addStretch()
        layout.addLayout(info_layout)

        # Connect drop zone signal
        self.drop_zone.files_dropped.connect(self.handle_files)

    def setup_conversion_watch(self):
        """Set up background conversion watch thread."""
        self.watch_thread = QThread()
        self.watch_thread.start()

    def on_files_dropped(self, files: list[str]):
        """Handle files dropped onto the drop zone."""
        for filepath in files:
            file_path = Path(filepath)
            if file_path.exists():
                self.status_label.setText(f"Converting: {file_path.name}")
                self.convert_file(file_path)

    def convert_file(self, file_path: Path):
        """Convert a single file."""
        try:
            success = self.engine.convert(file_path)
            if success:
                self.results_list.add_result(
                    file_path.name,
                    "success",
                    "Converted successfully"
                )
                self.status_label.setText(f"Completed: {file_path.name}")
            else:
                self.results_list.add_result(
                    file_path.name,
                    "error",
                    "Conversion failed or unsupported format"
                )
                self.status_label.setText(f"Failed: {file_path.name}")
        except Exception as e:
            self.results_list.add_result(
                file_path.name,
                "error",
                str(e)
            )
            self.status_label.setText(f"Error: {file_path.name}")

    def add_watch_directory(self):
        """Add a directory to watch for file changes."""
        directory = QFileDialog.getExistingDirectory(
            self,
            "Select Directory to Watch",
            str(Path.home()),
        )

        if directory:
            self.watch_dir = Path(directory)
            try:
                self.engine.start_watch(self.watch_dir)
                self.status_label.setText(f"Watching: {self.watch_dir}")
                self.results_list.add_result(
                    str(self.watch_dir),
                    "success",
                    "Now watching for file changes"
                )
            except Exception as e:
                QMessageBox.critical(
                    self,
                    "Error",
                    f"Failed to watch directory:\n{str(e)}"
                )

    def clear_results(self):
        """Clear the results list."""
        self.results_list.clear()
        self.status_label.setText("Results cleared")

    def closeEvent(self, event):
        """Handle application close."""
        self.engine.stop_watch()
        event.accept()


def create_main_window(config: ConfigLoader) -> MainWindow:
    """Create and return the main application window."""
    return MainWindow(config)
