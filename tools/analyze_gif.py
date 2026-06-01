from PIL import Image, ImageSequence

img = Image.open('tools/QQ20260531-013327-HD.gif')

print(f'文件: QQ20260531-013327-HD.gif')
print(f'尺寸: {img.size}')
print(f'格式: {img.format}')
print(f'模式: {img.mode}')

frames = list(ImageSequence.Iterator(img))
print(f'总帧数: {len(frames)}')

durations = [frame.info.get('duration', 0) for frame in frames]
print(f'各帧时长(ms): {durations}')
print(f'总时长: {sum(durations)/1000:.2f}秒')
print(f'文件大小: 2.93 MB')

# 检查是否有透明度
print(f'是否支持透明度: {"是" if img.mode in ("RGBA", "PA", "LA") or "transparency" in img.info else "否"}')