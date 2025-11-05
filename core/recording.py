from pynput import mouse, keyboard
import time

class Recorder:
    def __init__(self):
        self.events = []
        self.recording = False
        self.start_time = None
        self.last_position = None

    def start_recording(self):
        self.recording = True
        self.start_time = time.perf_counter()
        self.events = []

        self.mouse_listener = mouse.Listener(
            on_move=self.on_move,
            on_click=self.on_click,
            on_scroll=self.on_scroll,
        )
        self.keyboard_listener = keyboard.Listener(
            on_press=self.on_press,
            on_release=self.on_release
        )

        self.mouse_listener.start()
        self.keyboard_listener.start()

    def stop_recording(self):
        self.recording = False
        self.mouse_listener.stop()
        self.keyboard_listener.stop()

    def on_move(self, x, y):
        if self.recording:
            current_position = (x, y)
            delta = (0, 0)
            if self.last_position is not None:
                delta = (x - self.last_position[0], y - self.last_position[1])

            self.events.append({
                "type": "move",
                "position": current_position,
                "delta": delta,
                "last_position": self.last_position,
                "time": time.perf_counter() - self.start_time
            })
            self.last_position = current_position

    def on_click(self, x, y, button, pressed):
        if self.recording:
            event = {
                "type": "click",
                "position": (x, y),
                "button": str(button),
                "pressed": pressed,
                "time": time.perf_counter() - self.start_time
            }
            self.events.append(event)

            # Print click press or release
            action = "pressed" if pressed else "released"
            print(f"Mouse {action} at {x},{y} with {button}")

    def on_click_release(self, x, y, button, pressed):
        if self.recording:
            self.events.append({
                "type": "click_release",
                "position": (x, y),
                "button": str(button),
                "pressed": pressed,
                "time": time.perf_counter() - self.start_time
            })

    def on_scroll(self, x, y, dx, dy):
        if self.recording:
            self.events.append({
                "type": "scroll",
                "position": (x, y),
                "delta": (dx, dy),
                "time": time.perf_counter() - self.start_time
            })

    def on_press(self, key):
        if self.recording:
            self.events.append({
                "type": "key_press",
                "key": str(key),
                "time": time.perf_counter() - self.start_time
            })

    def on_release(self, key):
        if self.recording:
            self.events.append({
                "type": "key_release",
                "key": str(key),
                "time": time.perf_counter() - self.start_time
            })

if __name__ == "__main__":
    recorder = Recorder()
    print("Starting recording...")
    recorder.start_recording()
    time.sleep(10)  # Record for 10 seconds
    recorder.stop_recording()
    print("Recording stopped.")
    print("Recorded events:")
    for event in recorder.events:
        print(event)
