"""PyQt6 UI components for ReType."""

from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QPushButton, QLabel,
    QListWidget, QListWidgetItem, QMessageBox, QFileDialog,
)
from PyQt6.QtCore import Qt, QMimeData, pyqtSignal
from PyQt6.QtGui import QDragEnterEvent, QDropEvent


class DropZoneWidget(QWidget):
    """Drag and drop zone for files."""

    files_dropped = pyqtSignal(list)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setMinimumHeight(150)
        self.setStyleSheet("""
            DropZoneWidget {
                background-color: #2d2d2d;
                border: 2px dashed #555555;
                border-radius: 10px;
                color: #aaaaaa;
                font-size: 14px;
            }
            DropZoneWidget:hover {
                border-color: #0078d4;
                background-color: #333333;
            }
        """)

        layout = QVBoxLayout(self)
        layout.setAlignment(Qt.AlignmentFlag.AlignCenter)

        self.label = QLabel("Files to convert")
        self.label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(self.label)

        self.setAcceptDrops(True)

    def dragEnterEvent(self, event: QDragEnterEvent):
        if event.mimeData().hasUrls():
            event.acceptProposedAction()

    def dropEvent(self, event: QDropEvent):
        files = []
        for url in event.mimeData().urls():
            path = url.toLocalFile()
            if path and not path.endswith('/'):
                files.append(path)
        if files:
            self.files_dropped.emit(files)
        event.acceptProposedAction()


class ConversionResultItem:
    """Represents a single conversion result."""

    def __init__(self, filename, status, message):
        self.filename = filename
        self.status = status  # 'success', 'error', 'pending'
        self.message = message


class ResultsListWidget(QListWidget):
    """Widget showing conversion results."""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setMaximumHeight(300)
        self.setStyleSheet("""
            QListWidget {
                background-color: #1e1e1e;
                border: 1px solid #333333;
                border-radius: 5px;
                font-family: Consolas, Monaco, monospace;
                font-size: 12px;
            }
            QListWidget::item {
                padding: 5px;
                border-bottom: 1px solid #2d2d2d;
            }
            QListWidget::item:selected {
                background-color: #094551;
            }
        """)

    def add_result(self, filename: str, status: str, message: str):
        """Add a conversion result to the list."""
        icon = ""
        color = ""

        if status == "success":
            icon = "✓"
            color = "#4ec9b0"
        elif status == "error":
            icon = "✗"
            color = "#f44747"
        else:
            icon = "..."
            color = "#dcdcaa"

        item = QListWidgetItem(f"{icon} {filename} - {message}")
        item.setStyleSheet(f"color: {color};")
        self.addItem(item)

        # Scroll to bottom
        self.scrollToBottom()
