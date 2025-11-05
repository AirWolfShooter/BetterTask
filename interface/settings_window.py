import customtkinter as ctk
from interface.settings_manager import SettingsManager



class SettingsWindow(ctk.CTkToplevel):
    DEFAULT_FG = ctk.ThemeManager.theme["CTkButton"]["fg_color"]
    DEFAULT_HOVER = ctk.ThemeManager.theme["CTkButton"]["hover_color"]

    def __init__(self, master=None, settings_manager=None):
        super().__init__(master)

        self.title("Settings")
        self.geometry("600x600")
        self.attributes("-topmost", True)
        self.resizable(False, False)
        self.transient(master)

        self.continuous_playback_var = ctk.BooleanVar(value=False)
        self.minimize_to_tray_var = ctk.BooleanVar(value=False)
        self.minimalistic_mode_var = ctk.BooleanVar(value=False)
        self.saveOnChange_var = ctk.BooleanVar(value=False)

        self.settings_manager = settings_manager

        self.buttonWidth = 200
        self.buttonHeight = 40

        # Title
        self.settings_title = ctk.CTkLabel(self, text="Settings", font=ctk.CTkFont(size=18, weight="bold"))
        self.settings_title.place(relx=0.5, rely=0.07, anchor="center")

        underline = ctk.CTkFrame(self, height=2, width=200, fg_color="grey")
        underline.place(relx=0.5, rely=0.1, anchor="center")

        # Main container frame
        self.settings_frame = ctk.CTkFrame(self, corner_radius=10)
        self.settings_frame.place(relx=0.5, rely=0.55, anchor="center", relwidth=0.95, relheight=0.8)

        # Left scrollable sections
        self.section_scroll_frame = ctk.CTkScrollableFrame(self.settings_frame, corner_radius=10, width=230)
        self.section_scroll_frame.pack(side="left", fill="y", expand=False, padx=(10, 5), pady=10)

        # Right scrollable options area
        self.options_scroll_frame = ctk.CTkScrollableFrame(self.settings_frame, corner_radius=10)
        self.options_scroll_frame.pack(side="left", fill="both", expand=True, padx=(5, 10), pady=10)

        # Define sections
        self.sections = {
            "General": self.show_general,
            "Misc": self.show_misc}

                # Track current selected button
        self.active_button = None

        # Create sidebar buttons
        for name, func in self.sections.items():
            btn = ctk.CTkButton(
                self.section_scroll_frame,
                    text=name,
                    height=40,
                    font=ctk.CTkFont(size=14),
                    fg_color=self.DEFAULT_FG,
                    hover_color=self.DEFAULT_HOVER,
                    command=lambda f=func, n=name: self.switch_section(f, n)
                    )
            btn.pack(pady=6, padx=10, fill="x")

        # Load default section
            first_section = next(iter(self.sections))
            self.switch_section(self.sections[first_section], first_section)

    def switch_section(self, func, name):
        # Reset button colors
        for child in self.section_scroll_frame.winfo_children():
            if isinstance(child, ctk.CTkButton):
                child.configure(fg_color=self.DEFAULT_FG, hover_color=self.DEFAULT_HOVER)

        # Highlight active
        for child in self.section_scroll_frame.winfo_children():
            if isinstance(child, ctk.CTkButton) and child.cget("text") == name:
                child.configure(fg_color=("#228B22", "#228B22"), hover_color=("#005700", "#005700"))
                self.active_button = child

        # Clear right frame
        for widget in self.options_scroll_frame.winfo_children():
            widget.destroy()

                # Build section content
        func()

            # Section builders
    def show_general(self):
        ctk.CTkLabel(self.options_scroll_frame, text="General Settings", font=ctk.CTkFont(size=16, weight="bold")).pack(pady=(0,10), anchor="w")
        ctk.CTkCheckBox(self.options_scroll_frame, text="Continuous Playback", variable=self.continuous_playback_var).pack(pady=5, anchor="w")
        ctk.CTkCheckBox(self.options_scroll_frame, text="Minimize to tray", variable=self.minimize_to_tray_var).pack(pady=5, anchor="w")
        ctk.CTkCheckBox(self.options_scroll_frame, text="Enable Minimalistic Mode", variable=self.minimalistic_mode_var).pack(pady=5, anchor="w")
        ctk.CTkButton(self.options_scroll_frame, width=self.buttonWidth, height=self.buttonHeight, text="Change Hotkeys").pack(pady=10, padx=20, anchor="w")
        ctk.CTkButton(self.options_scroll_frame, width=self.buttonWidth, height=self.buttonHeight, text="Open Settings Manager",command=self.open_settings_manager).pack(pady=10, padx=20, anchor="w")
        ctk.CTkCheckBox(self.options_scroll_frame, text="Auto Save Settings on Change", variable=self.saveOnChange_var).pack(pady=5, anchor="w")

    def show_misc(self):
        ctk.CTkLabel(self.options_scroll_frame, text="Miscellaneous Settings", font=ctk.CTkFont(size=16, weight="bold")).pack(pady=(0,10), anchor="w")
        ctk.CTkLabel(self.options_scroll_frame, text="Version: 1.0.0").pack(pady=5, anchor="w")
        ctk.CTkLabel(self.options_scroll_frame, text="Developer: AirWolfShooter").pack(pady=5, anchor="w")
        ctk.CTkButton(self.options_scroll_frame, width=self.buttonWidth, height=self.buttonHeight, text="Check for Updates").pack(pady=10, padx=20, anchor="w")
        ctk.CTkButton(self.options_scroll_frame, width=self.buttonWidth, height=self.buttonHeight, text="View Logs").pack(pady=5, padx=20, anchor="w")

    def open_settings_manager(self):
        # Create or retrieve the manager, then ensure it's visible and focused.
        if self.settings_manager is None or not self.settings_manager.winfo_exists():
            # This will return the existing singleton instance if present.
            self.settings_manager = SettingsManager(self)

        # Ensure it's visible and on top.
        try:
            self.settings_manager.deiconify()
        except Exception:
            pass
        try:
            self.settings_manager.lift()
            self.settings_manager.focus_force()
        except Exception:
            pass