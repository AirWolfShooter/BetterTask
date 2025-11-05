import os
import json
import customtkinter as ctk
from tkinter import filedialog, Menu
from CTkMessagebox import CTkMessagebox

APPDATA_DIR = os.path.join(os.getenv("APPDATA"), "Better-Task", "settings", "saves")
LAST_USED_FILE = os.path.join(APPDATA_DIR, "last_used.txt")
os.makedirs(APPDATA_DIR, exist_ok=True)

class SettingsManager(ctk.CTkToplevel):
    instance = None

    

    def __new__(cls, parent=None, refresh_interval_ms: int = 1000):
        # If an instance already exists and the window still exists, return it
        # so callers get the same Toplevel instead of creating a new, partially
        # initialized object.
        if cls.instance is not None and cls.instance.winfo_exists():
            cls.instance.lift()
            cls.instance.focus_force()
            return cls.instance
        return super().__new__(cls)

    def __init__(self, master=None, refresh_interval_ms: int = 1000):
        # Prevent re-initialization of an already-created instance. When
        # __new__ returns an existing instance, Python will still call
        # __init__ again — skip the heavy init in that case.
        if getattr(self, "_initialized", False):
            # bring to front and return
            try:
                self.lift()
                self.focus_force()
            except Exception:
                pass
            return

        super().__init__(master)
        SettingsManager.instance = self
        self.parent = master
        self.title("Settings Manager")
        self.geometry("460x500")
        self.resizable(False, False)
        self.wm_attributes("-topmost", True)
        self.protocol("WM_DELETE_WINDOW", self.on_close)
        self.transient(master)

        self.DEFAULT_FG = ctk.ThemeManager.theme["CTkButton"]["fg_color"]
        self.DEFAULT_HOVER = ctk.ThemeManager.theme["CTkButton"]["hover_color"]
        # Selected / active file colors
        self.SELECTED_FG = "#228B22"
        self.SELECTED_HOVER = "#005700"

        self._last_files = set()
        self._refresh_interval_ms = refresh_interval_ms
        self._selected_file = None
        self._suspend_traces = False
        self._dirty_files = set()

        self.bool_settings = {}
        bool_names = [
            "continuous_playback",
            "minimize_to_tray",
            "minimalistic_mode",
            "saveOnChange"
        ]
        for name in bool_names:
            var_name = f"{name}_var"
            if master and hasattr(master, var_name):
                var = getattr(master, var_name)
            else:
                var = ctk.BooleanVar(master=self, value=False)
            self.bool_settings[name] = var

        for name, var in self.bool_settings.items():
            var.trace_add("write", lambda *a, n=name: self.on_bool_change(n))

        if master:
            self.setup_ui()
        self.refresh_list()
        self.schedule_refresh()

        self._initialized = True

    def setup_ui(self):
        ctk.CTkLabel(self, text="Settings Manager", font=("Arial", 18, "bold")).pack(pady=(10, 5))
        self.scroll_frame = ctk.CTkScrollableFrame(self)
        self.scroll_frame.pack(fill="both", expand=True, padx=12, pady=(0, 10))

        button_frame = ctk.CTkFrame(self)
        button_frame.pack(fill="x", padx=12, pady=(0, 12))
        ctk.CTkButton(button_frame, text="Edit Current Settings file", command=self.edit_current).pack(
            side="left", expand=True, padx=6, pady=6
        )
        ctk.CTkButton(button_frame, text="Import Settings", command=self.import_settings).pack(
            side="left", expand=True, padx=6, pady=6
        )
        ctk.CTkButton(button_frame, text="Export Settings", command=self.export_settings).pack(
            side="left", expand=True, padx=6, pady=6
        )

        new_frame = ctk.CTkFrame(self)
        new_frame.pack(fill="x", padx=12, pady=(0, 12))
        ctk.CTkLabel(new_frame, text="New Settings Filename:").pack(side="left", padx=(6, 2))
        self.new_file_entry = ctk.CTkEntry(new_frame)
        self.new_file_entry.pack(side="left", fill="x", expand=True, padx=(0, 6))
        ctk.CTkButton(new_frame, text="Create New", command=self.create_new).pack(side="left", padx=(0, 6))

    def refresh_list(self):
        try:
            files = {f for f in os.listdir(APPDATA_DIR) if f.endswith(".json")}
        except FileNotFoundError:
            files = set()
        if files == self._last_files:
            for widget in self.scroll_frame.winfo_children():
                try:
                    text = widget.cget("text")
                except Exception:
                    continue
                path = os.path.join(APPDATA_DIR, text)
                try:
                    is_selected = bool(self._selected_file) and os.path.normpath(self._selected_file) == os.path.normpath(path)
                except Exception:
                    is_selected = False
                if is_selected:
                    widget.configure(fg_color=self.SELECTED_FG, hover_color=self.SELECTED_HOVER)
                else:
                    widget.configure(fg_color=self.DEFAULT_FG, hover_color=self.DEFAULT_HOVER)
            return
        self._last_files = files
        for widget in self.scroll_frame.winfo_children():
            widget.destroy()

        for file in sorted(files):
            path = os.path.join(APPDATA_DIR, file)
            btn = ctk.CTkButton(self.scroll_frame, text=file, anchor="w", command=lambda p=path: self.switch_file(p))
            btn.pack(fill="x", padx=6, pady=3)

            # Apply selected/unselected colors. Use normalized paths for comparison
            try:
                is_selected = bool(self._selected_file) and os.path.normpath(self._selected_file) == os.path.normpath(path)
            except Exception:
                is_selected = False
            if is_selected:
                btn.configure(fg_color=self.SELECTED_FG, hover_color=self.SELECTED_HOVER)
            else:
                btn.configure(fg_color=self.DEFAULT_FG, hover_color=self.DEFAULT_HOVER)

            menu = Menu(self, tearoff=0)
            menu.add_command(label="Delete", command=lambda f=path: self.delete_settings_file(f))
            btn.bind("<Button-3>", lambda e, m=menu: m.tk_popup(e.x_root, e.y_root))

    def switch_file(self, path):
        if self._selected_file in self._dirty_files:
            self.save_file(self._selected_file)
        self.load_settings(path)

    def load_settings(self, path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
            self._suspend_traces = True
            for key, var in self.bool_settings.items():
                var.set(bool(data.get(key, False)))
            self._selected_file = path
            self.write_last_used(path)
            print(f"[INFO] Settings loaded: {path}")
            self.refresh_list()
        except Exception as e:
            print(f"[ERROR] Failed to load settings: {e}")
            CTkMessagebox(title="Error", message=f"Failed to load settings: {e}", icon="cancel")
        finally:
            self._suspend_traces = False

    def save_file(self, path):
        try:
            data = {k: v.get() for k, v in self.bool_settings.items()}
            with open(path, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2)
            if path in self._dirty_files:
                self._dirty_files.remove(path)
            print(f"[INFO] Saved settings: {path}")
        except Exception as e:
            print(f"[ERROR] Failed to save file: {e}")

    def edit_current(self):
        if not self._selected_file:
            CTkMessagebox(title="Warning", message="No settings selected to edit.", icon="warning")
            return
        self.save_file(self._selected_file)
        self.write_last_used(self._selected_file)
        CTkMessagebox(title="Saved", message="Current settings file updated.", icon="check")

    def create_new(self):
        filename = self.new_file_entry.get().strip()
        if not filename:
            CTkMessagebox(title="Warning", message="Enter a name for the new settings file.", icon="warning")
            return
        path = os.path.join(APPDATA_DIR, filename + ".json")
        if os.path.exists(path):
            msg = CTkMessagebox(title="Overwrite?", message="File exists. Overwrite?", icon="question", option_1="Cancel", option_2="Yes")
            if msg.get() != "Yes":
                return
        self.save_file(path)
        self._selected_file = path
        self.write_last_used(path)
        self.refresh_list()
        CTkMessagebox(title="Saved", message=f"New settings saved as {filename}.json", icon="check")

    def import_settings(self):
        file_path = filedialog.askopenfilename(title="Select Settings JSON", filetypes=[("JSON Files", "*.json")])
        if not file_path:
            return
        with open(file_path, "r", encoding="utf-8") as f:
            data = json.load(f)
        name = os.path.splitext(os.path.basename(file_path))[0]
        dest_path = os.path.join(APPDATA_DIR, f"{name}.json")
        counter = 1
        while os.path.exists(dest_path):
            dest_path = os.path.join(APPDATA_DIR, f"{name}_{counter}.json")
            counter += 1
        with open(dest_path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2)
        self._selected_file = dest_path
        self.write_last_used(dest_path)
        CTkMessagebox(title="Imported", message=f"Settings imported as '{os.path.basename(dest_path)}'", icon="check")
        self.refresh_list()

    def export_settings(self):
        if not self._selected_file:
            CTkMessagebox(title="Warning", message="No settings selected to export.", icon="warning")
            return
        export_path = filedialog.asksaveasfilename(title="Export Settings As", defaultextension=".json", filetypes=[("JSON Files", "*.json")])
        if not export_path:
            return
        with open(self._selected_file, "r", encoding="utf-8") as f:
            data = json.load(f)
        with open(export_path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2)
        CTkMessagebox(title="Exported", message=f"Settings exported to {export_path}", icon="check")

    def delete_settings_file(self, path):
        msg = CTkMessagebox(title="Delete?", message=f"Delete {os.path.basename(path)}?", icon="question", option_1="Cancel", option_2="Yes")
        if msg.get() != "Yes":
            return
        try:
            os.remove(path)
            if self._selected_file and os.path.normpath(self._selected_file) == os.path.normpath(path):
                self._selected_file = None
            if path in self._dirty_files:
                self._dirty_files.remove(path)
            self.refresh_list()
        except Exception as e:
            CTkMessagebox(title="Error", message=f"Failed to delete: {e}", icon="cancel")

    def on_close(self):
        SettingsManager.instance = None
        self.destroy()

    def schedule_refresh(self):
        if not self.winfo_exists():
            return
        self.refresh_list()
        self.after(self._refresh_interval_ms, self.schedule_refresh)

    def on_bool_change(self, changed_name):
        if self._suspend_traces:
            return
        var = self.bool_settings.get(changed_name)
        print(f"[DEBUG] {changed_name} changed -> {var.get()}")
        if self._selected_file and self.bool_settings.get("saveOnChange").get():
            self.save_file(self._selected_file)
        else:
            self._dirty_files.add(self._selected_file)

    def write_last_used(self, path):
        try:
            with open(LAST_USED_FILE, "w", encoding="utf-8") as f:
                f.write(os.path.basename(path))
            print(f"[INFO] Last used settings saved: {LAST_USED_FILE}")
        except Exception as e:
            print(f"[ERROR] Failed writing last used file: {e}")

    def load_last_used(self):
        if not os.path.exists(LAST_USED_FILE):
            print("[INFO] No last-used file found")
            return
        try:
            with open(LAST_USED_FILE, "r", encoding="utf-8") as f:
                last_file = f.read().strip()
            candidate = os.path.join(APPDATA_DIR, last_file)
            if os.path.exists(candidate):
                print(f"[INFO] Loading last-used settings: {candidate}")
                self.load_settings(candidate)
            else:
                print(f"[WARNING] Last-used file does not exist: {candidate}")
        except Exception as e:
            print(f"[ERROR] Failed to load last-used file: {e}")
