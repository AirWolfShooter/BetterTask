import screeninfo
import threading
import time

# monitor state variables
_min_x, _min_y, _total_width, _total_height = 0, 0, 1, 1  # defaults
_monitor_lock = threading.Lock()


def capture_monitor_state():
    monitors = screeninfo.get_monitors()
    min_x = min(m.x for m in monitors)
    min_y = min(m.y for m in monitors)
    max_x = max(m.x + m.width for m in monitors)
    max_y = max(m.y + m.height for m in monitors)
    return min_x, min_y, max_x - min_x, max_y - min_y


def refresh_monitor_state():
    global _min_x, _min_y, _total_width, _total_height
    with _monitor_lock:
        _min_x, _min_y, _total_width, _total_height = capture_monitor_state()


def start_monitor_watcher(interval: float = 1.0):
    def watcher():
        global _min_x, _min_y, _total_width, _total_height
        prev_state = None
        while True:
            state = capture_monitor_state()
            if state != prev_state:
                with _monitor_lock:
                    _min_x, _min_y, _total_width, _total_height = state
                print(f"[MonitorWatcher] Resolution changed → {state[2]}x{state[3]}")
                prev_state = state
            time.sleep(interval)

    threading.Thread(target=watcher, daemon=True).start()


def normalize_coords(x: int, y: int) -> tuple[float, float]:
    with _monitor_lock:
        nx = (x - _min_x) / _total_width
        ny = (y - _min_y) / _total_height
    return nx, ny


def denormalize_coords(nx: float, ny: float) -> tuple[int, int]:
    with _monitor_lock:
        x = int(nx * _total_width + _min_x)
        y = int(ny * _total_height + _min_y)
    return x, y


# Check once at startup
refresh_monitor_state()
# Start background watcher (updates if resolution/monitors change)
start_monitor_watcher()
