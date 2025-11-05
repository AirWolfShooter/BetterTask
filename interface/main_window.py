import time
import customtkinter as ctk
import tkinter as tk
import logging
import pydirectinput
from core.recording import Recorder
from core.playback import Playback
from interface.settings_window import SettingsWindow
from interface.settings_manager import SettingsManager
import threading

# Gonna try to make hotkeys work in here prob just gonna connect them to the toggle functions as it will prob work the best (this is gonna be a headache)

class MainWindow(ctk.CTk):
    DEFAULT_FG = ctk.ThemeManager.theme["CTkButton"]["fg_color"]
    DEFAULT_HOVER = ctk.ThemeManager.theme["CTkButton"]["hover_color"]

    def __init__(self):
        super().__init__()

        self.title("Better Task")
        self.geometry("240x440")
        self.resizable(False, False)
        self.attributes("-topmost", True)

        self.seconds = 0
        self.running = False
        self.playing_timer_running = False
        self.start_time = None
        self.loop_count = 0

        self.recorder = Recorder()
        self.playback = Playback(events=self.recorder.events)


        self.settings_window = SettingsWindow(self)
        self.settings_window.withdraw()

        self.settings_manager = SettingsManager(self.settings_window)
        self.settings_manager.withdraw()

        # Load last-used settings once (do not let each window auto-load)
        self.settings_manager.load_last_used()

        try:
            self.iconbitmap("assets/Better-Task_Logo.ico")
        except Exception as e:
            print(f"Failed to set icon: {e}")
        
        self.build_interface()

    def build_interface(self):
        self.title_label = ctk.CTkLabel(
            self, text="Better Task",
            font=ctk.CTkFont(size=18, weight="bold")
        )
        self.title_label.pack(pady=(20, 10))

        self.inner_frame = ctk.CTkFrame(self, width=220, height=140, corner_radius=10)
        self.inner_frame.pack_propagate(False)
        self.inner_frame.pack(pady=(15, 10))

        hotkeys_text = "Record: F9  |  Playback: F10"
        self.current_hotkey_label = ctk.CTkLabel(self.inner_frame, text=hotkeys_text, font=ctk.CTkFont(size=12))
        self.current_hotkey_label.pack(fill="y", expand=True)

        self.timer_label = ctk.CTkLabel(self.inner_frame, text="Timer: 00:00:00", font=ctk.CTkFont(size=12, weight="bold"))
        self.timer_label.pack(fill="y", expand=True)

        self.loop_label = ctk.CTkLabel(self.inner_frame, text="Loops: 0", font=ctk.CTkFont(size=12, weight="bold"))
        self.loop_label.pack(fill="y", expand=True)

        self.events_label = ctk.CTkLabel(self.inner_frame, text="Events Recorded: 0", font=ctk.CTkFont(size=12))
        self.events_label.pack(fill="y", expand=True)

        separator = ctk.CTkFrame(self, height=2, fg_color="gray40")
        separator.pack(fill="x", padx=10, pady=(0, 15))

        button_width, button_height, button_pad_y = 160, 37, 5

        self.record_button = ctk.CTkButton(
            self, text="⏺️ Record",
            fg_color=self.DEFAULT_FG,
            hover_color=self.DEFAULT_HOVER,
            width=button_width, height=button_height,
            command=self.toggle_recording
        )
        self.record_button.pack(pady=(button_pad_y, button_pad_y))

        self.play_button = ctk.CTkButton(
            self, text="▶️ Start Playback",
            fg_color=self.DEFAULT_FG,
            hover_color=self.DEFAULT_HOVER,
            width=button_width, height=button_height,
            command=self.toggle_playback
        )
        self.play_button.pack(pady=(button_pad_y, button_pad_y))

        self.macro_button = ctk.CTkButton(
            self, text="🎦 Macro Manager",
            fg_color=self.DEFAULT_FG,
            hover_color=self.DEFAULT_HOVER,
            width=button_width, height=button_height
        )
        self.macro_button.pack(pady=(button_pad_y, button_pad_y))

        self.settings_button = ctk.CTkButton(
            self, text="⚙️ Settings",
            fg_color=self.DEFAULT_FG,
            hover_color=self.DEFAULT_HOVER,
            width=button_width, height=button_height,
            command=self.open_settings
        )
        self.settings_button.pack(pady=(button_pad_y, button_pad_y))

        self.check_geometry()

    def check_geometry(self):
        current_geometry = self.geometry().split('+')[0]
        if current_geometry == "240x440":
            print("The window is 240x440 pixels — using normal layout.")
        else:
            print(f"Current geometry: {current_geometry} — use minimized layout.")
        self.after(1000, self.check_geometry)

    def update_timer(self):
        if self.running:
            elapsed = int(time.time() - self.start_time)
            h, m, s = elapsed // 3600, (elapsed % 3600) // 60, elapsed % 60
            self.timer_label.configure(text=f"Timer: {h:02}:{m:02}:{s:02}")
            self.after(200, self.update_timer)

    def update_playback_timer(self):
        if self.playing_timer_running:
            elapsed = int(time.time() - self.start_time)
            h, m, s = elapsed // 3600, (elapsed % 3600) // 60, elapsed % 60
            self.timer_label.configure(text=f"Timer: {h:02}:{m:02}:{s:02}")
            self.after(200, self.update_playback_timer)

    def update_events(self):
        self.events_label.configure(text=f"Events Recorded: {len(self.recorder.events)}")
        if self.running:
            self.after(10, self.update_events)

    def open_settings(self):
        if self.settings_window is None or not self.settings_window.winfo_exists():
            self.settings_window = SettingsWindow(self)
        else:
            self.settings_window.focus_force()
            self.settings_window.deiconify()

    def toggle_recording(self):
        if not self.running:
            threading.Thread(target=self.recorder.start_recording, daemon=True).start()
            self.record_button.configure(text="⏹️ Stop Recording")
            self.seconds = 0
            self.running = True
            self.start_time = time.time()
            self.timer_label.configure(text="Timer: 00:00:00")
            self.update_timer()
            self.update_events()
        else:
            self.recorder.stop_recording()
            self.running = False
            self.record_button.configure(text="⏺️ Record")
            self.timer_label.configure(text="Timer: 00:00:00")

    def toggle_playback(self):
        if not self.playback.playing:
            self.playback.events = self.recorder.events
            self.play_button.configure(text="⏹️ Stop Playback")
            self.record_button.configure(state="disabled")
            self.start_time = time.time()
            self.playing_timer_running = True
            self.loop_count = 0
            self.loop_label.configure(text=f"Loops: {self.loop_count}")
            self.update_playback_timer()

            def run_playback():
                first_loop = True
                while first_loop or self.settings_window.continuous_playback_var.get():
                    first_loop = False
                    self.playback.start_playback()
                    self.playback.playing = False
                    self.loop_count += 1
                    self.after(0, lambda count=self.loop_count: self.loop_label.configure(text=f"Loops: {count}"))
                    pydirectinput.mouseUp(button="left")
                    pydirectinput.mouseUp(button="right")

                self.playing_timer_running = False
                self.after(0, lambda: (
                    self.play_button.configure(text="▶️ Start Playback"),
                    self.record_button.configure(state="normal"),
                    self.timer_label.configure(text="Timer: 00:00:00"),
                    self.loop_label.configure(text="Loops: 0")
                ))

            self.playback_thread = threading.Thread(target=run_playback, daemon=True)
            self.playback_thread.start()
        else:
            self.playback.stop_playback()
            self.play_button.configure(text="▶️ Start Playback")

if __name__ == "__main__":
    app = MainWindow()
    app.mainloop()
