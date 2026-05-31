import os

base = r'd:\Prophecy-Century\GameProject\prophecy_century\prophecy-century-Unity'
gif_path = os.path.join(base, 'tools', 'QQ20260531-013327-HD.gif')
out_dir = os.path.join(base, 'tools', 'gif_frames')
log_path = os.path.join(base, 'tools', '_result.txt')

log = open(log_path, 'w', encoding='utf-8')

log.write(f'GIF path: {gif_path}\n')
log.write(f'GIF exists: {os.path.exists(gif_path)}\n')
log.flush()

os.makedirs(out_dir, exist_ok=True)

from PIL import Image, ImageSequence

img = Image.open(gif_path)
log.write(f'Size: {img.size}\n')
log.write(f'Format: {img.format}\n')
log.write(f'Mode: {img.mode}\n')

frames = list(ImageSequence.Iterator(img))
log.write(f'Frames: {len(frames)}\n')

durations = [f.info.get('duration', 0) for f in frames]
log.write(f'Durations(ms): {durations}\n')
log.write(f'Total duration: {sum(durations)/1000:.2f}s\n')
log.write(f'Has transparency: {"transparency" in img.info}\n')
log.flush()

for i, frame in enumerate(frames):
    fpath = os.path.join(out_dir, f'frame_{i+1:03d}.png')
    frame.save(fpath, 'PNG')
    if i % 10 == 0:
        log.write(f'Saved frame {i+1}/{len(frames)}\n')
        log.flush()

log.write(f'ALL DONE: {len(frames)} frames to {out_dir}\n')
log.close()
print('DONE')