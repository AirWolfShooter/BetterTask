import pydirectinput
import time
from pynput.mouse import Controller as MouseController
from core.recording import Recorder


class Playback:
    def __init__(self, events):
        self.events = events or []
        if not self.events:
            print("No events to play back.")
        self.playing = False
        pydirectinput.PAUSE = 0.001
        self.mouse_controller = MouseController()

    def interruptible_sleep(self, duration):
        end_time = time.monotonic() + duration
        while self.playing:
            remaining = end_time - time.monotonic()
            if remaining <= 0:
                break
            time.sleep(min(0.01, remaining))

    def start_playback(self):
        if not self.events:
            return
        self.playing = True
        print("Starting playback...")
        self.play_events()
        print("Playback finished.")

    def stop_playback(self):
        self.playing = False

    def play_events(self):
        start_time = time.perf_counter()

        for event in self.events:
            if not self.playing:
                break

            wait_time = event["time"] - (time.perf_counter() - start_time)
            if wait_time > 0:
                self.interruptible_sleep(wait_time)

            if not self.playing:
                break

            self.play_event(event)

    def play_event(self, event):
        etype = event["type"]

        if etype == "move":
            x, y = event["position"]
            pydirectinput.moveTo(int(x), int(y))

        elif etype == "click":
            x, y = event["position"]
            pydirectinput.moveTo(int(x), int(y))
            button = str(event["button"]).lower()
            pressed = event["pressed"]

            if "left" in button:
                if pressed:
                    pydirectinput.moveTo(int(x), int(y))
                    pydirectinput.mouseDown(button="left")
                else:
                    pydirectinput.moveTo(int(x), int(y))
                    pydirectinput.mouseUp(button="left")
            elif "right" in button:
                if pressed:
                    pydirectinput.moveTo(int(x), int(y))
                    pydirectinput.mouseDown(button="right")
                else:
                    pydirectinput.moveTo(int(x), int(y))
                    pydirectinput.mouseUp(button="right")

        elif etype == "scroll":
            x, y = event["position"]
            dx, dy = event["delta"]
            pydirectinput.moveTo(int(x), int(y))
            self.mouse_controller.scroll(0, int(dy * 10))

        elif etype == "key_press":
            key = str(event["key"]).replace("'", "").replace("Key.", "")
            try:
                pydirectinput.keyDown(key)
            except Exception as e:
                print(f"[WARN] Could not press key {key}: {e}")

        elif etype == "key_release":
            key = str(event["key"]).replace("'", "").replace("Key.", "")
            try:
                pydirectinput.keyUp(key)
            except Exception as e:
                print(f"[WARN] Could not release key {key}: {e}")

        else:
            print(f"Unknown event type: {etype}")
