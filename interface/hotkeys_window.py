import customtkinter as ctk

class HotkeysWindow(ctk.CTkToplevel):
    DEFAULT_FG = ctk.ThemeManager.theme["CTkButton"]["fg_color"]
    DEFAULT_HOVER = ctk.ThemeManager.theme["CTkButton"]["hover_color"]

    def __init__(self, master=None):
        super().__init__(master)

        self.title("Hotkey Manager")
        self.geometry("400x450")
        self.attributes("-topmost", True)
        self.resizable(False, False)
        self.transient(master)
        self.main_frame = ctk.CTkFrame(self, corner_radius=10)
        self.main_frame.pack(fill="both", expand=True, padx=10, pady=10)
        self.scrollable_frame = ctk.CTkScrollableFrame(self.main_frame, corner_radius=10)
        self.scrollable_frame.pack(fill="both", expand=True, padx=10, pady=10)


if __name__ == "__main__":
    app = HotkeysWindow()
    app.mainloop()