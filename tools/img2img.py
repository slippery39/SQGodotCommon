"""img2img through ComfyUI: an init image + prompt at a given denoise. Keeps the init's design."""
import io
import json
import sys
import time
import urllib.request
import uuid

from PIL import Image

SERVER = "http://127.0.0.1:8188"


def upload(img, name):
    buf = io.BytesIO()
    img.save(buf, "PNG")
    boundary = uuid.uuid4().hex
    body = (
        f'--{boundary}\r\nContent-Disposition: form-data; name="image"; filename="{name}"\r\n'
        f"Content-Type: image/png\r\n\r\n"
    ).encode() + buf.getvalue() + (
        f'\r\n--{boundary}\r\nContent-Disposition: form-data; name="overwrite"\r\n\r\ntrue\r\n'
        f"--{boundary}--\r\n"
    ).encode()
    req = urllib.request.Request(
        SERVER + "/upload/image", data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
    )
    return json.loads(urllib.request.urlopen(req).read())["name"]


def run(init, prompt, negative, denoise, seed, out):
    name = upload(init, f"i2i_{uuid.uuid4().hex[:6]}.png")
    g = {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": "dreamshaperXL_turbo.safetensors"}},
        "2": {"class_type": "LoadImage", "inputs": {"image": name}},
        "3": {"class_type": "VAEEncode", "inputs": {"pixels": ["2", 0], "vae": ["1", 2]}},
        "4": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": prompt}},
        "5": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": negative}},
        "6": {"class_type": "KSampler", "inputs": {
            "model": ["1", 0], "positive": ["4", 0], "negative": ["5", 0], "latent_image": ["3", 0],
            "seed": seed, "steps": 10, "cfg": 2.5, "sampler_name": "dpmpp_sde", "scheduler": "karras",
            "denoise": denoise}},
        "7": {"class_type": "VAEDecode", "inputs": {"samples": ["6", 0], "vae": ["1", 2]}},
        "8": {"class_type": "SaveImage", "inputs": {"images": ["7", 0], "filename_prefix": "i2i"}},
    }
    req = urllib.request.Request(SERVER + "/prompt", data=json.dumps({"prompt": g}).encode(),
                                 headers={"Content-Type": "application/json"})
    pid = json.loads(urllib.request.urlopen(req).read())["prompt_id"]
    while True:
        h = json.loads(urllib.request.urlopen(f"{SERVER}/history/{pid}").read())
        if pid in h:
            break
        time.sleep(1)
    e = h[pid]
    if e.get("status", {}).get("status_str") == "error":
        raise RuntimeError(json.dumps(e["status"])[:600])
    img = [i for n in e["outputs"].values() for i in n.get("images", [])][0]
    raw = urllib.request.urlopen(
        f"{SERVER}/view?filename={img['filename']}&subfolder={img.get('subfolder', '')}&type={img['type']}"
    ).read()
    open(out, "wb").write(raw)


if __name__ == "__main__":
    # jobs json: [{"init": path, "crop": [l,t,r,b] or null, "prompt", "negative", "denoise", "seed", "out"}]
    for j in json.load(open(sys.argv[1])):
        im = Image.open(j["init"]).convert("RGB")
        if j.get("crop"):
            im = im.crop(tuple(j["crop"]))
        side = max(im.size)
        sq = Image.new("RGB", (side, side), (255, 255, 255))
        sq.paste(im, ((side - im.width) // 2, (side - im.height) // 2))
        t = time.time()
        run(sq.resize((1024, 1024), Image.LANCZOS), j["prompt"], j["negative"], j["denoise"], j["seed"], j["out"])
        print(f"{time.time() - t:5.1f}s {j['out']}")
