import os, sys

# 使用绝对路径
base_dir = r'd:\Prophecy-Century\GameProject\prophecy_century\prophecy-century-Unity'
gif_path = os.path.join(base_dir, 'tools', 'QQ20260531-013327-HD.gif')
output_dir = os.path.join(base_dir, 'tools', 'gif_frames')

print(f'GIF路径: {gif_path}')
print(f'GIF存在: {os.path.exists(gif_path)}')
print(f'输出目录: {output_dir}')

os.makedirs(output_dir, exist_ok=True)

from PIL import Image, ImageSequence

try:
    img = Image.open(gif_path)
    print(f'GIF打开成功: 尺寸={img.size}, 格式={img.format}, 模式={img.mode}')
    
    frames = list(ImageSequence.Iterator(img))
    print(f'总帧数: {len(frames)}')
    
    durations = [frame.info.get('duration', 0) for frame in frames]
    print(f'各帧时长(ms): {durations}')
    print(f'总时长: {sum(durations)/1000:.2f}秒')
    
    # 写入信息文件
    info_path = os.path.join(base_dir, 'tools', 'gif_info.txt')
    with open(info_path, 'w', encoding='utf-8') as f:
        f.write(f'文件: QQ20260531-013327-HD.gif\n')
        f.write(f'尺寸: {img.size}\n')
        f.write(f'格式: {img.format}\n')
        f.write(f'模式: {img.mode}\n')
        f.write(f'总帧数: {len(frames)}\n')
        f.write(f'各帧时长(ms): {durations}\n')
        f.write(f'总时长: {sum(durations)/1000:.2f}秒\n')
        has_transparency = ('transparency' in img.info)
        f.write(f'包含透明色: {has_transparency}\n')
    print(f'信息已写入: {info_path}')
    
    # 提取每帧为 PNG
    for i, frame in enumerate(frames):
        frame_path = os.path.join(output_dir, f'frame_{i+1:03d}.png')
        frame.save(frame_path, 'PNG')
    print(f'共提取 {len(frames)} 帧到 {output_dir}')
    
    # 验证
    png_files = [f for f in os.listdir(output_dir) if f.endswith('.png')]
    print(f'输出目录包含 {len(png_files)} 个PNG文件')

except Exception as e:
    print(f'错误: {e}')
    import traceback
    traceback.print_exc()