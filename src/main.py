"""ReType - File Extension Converter.

A lightweight application that automatically converts files when you
change their extension in Windows Explorer.

Usage:
    python -m src.main
    or
    python src/main.py
"""

import sys
from pathlib import Path

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from PyQt6.QtWidgets import QApplication
from src.utils.config_loader import ConfigLoader
from src.utils.logger import setup_logging
from src.ui.main_window import create_main_window


def main():
    """Main entry point for ReType."""
    # Setup logging
    log_dir = Path.home() / ".local" / "share" / "retype" / "logs"
    setup_logging(log_dir, level="INFO")

    # Load configuration
    config_path = Path(__file__).parent.parent.parent / "config" / "settings.yaml"
    config = ConfigLoader(config_path)

    # Create and show application
    app = QApplication(sys.argv)
    app.setApplicationName("ReType")
    app.setOrganizationName("ReType")

    # Set application style
    app.setStyle("Fusion")

    # Create main window
    window = create_main_window(config)
    window.show()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
