import struct, os, sys

BASE = r'd:\Prophecy-Century\GameProject\prophecy_century\prophecy-century-Unity'
GIF = os.path.join(BASE, 'tools', 'QQ20260531-013327-HD.gif')
OUT = os.path.join(BASE, 'tools', '_gif_data.txt')

try:
    f = open(GIF, 'rb')
    data = f.read()
    f.close()
    
    hdr = data[:6].decode('ascii', errors='replace')
    w = struct.unpack_from('<H', data, 6)[0]
    h = struct.unpack_from('<H', data, 8)[0]
    
    # Count frames by scanning for 0x2C (image descriptor) markers
    frames = 0
    i = 13
    n = len(data)
    while i < n:
        b = data[i]
        if b == 0x2C:
            frames += 1
            i += 1
        elif b == 0x21:  # Extension
            i += 1
            if i >= n: break
            lbl = data[i]
            i += 1
            if lbl == 0xF9:
                i += 6
            elif lbl in (0xFE, 0x01):
                pass
            elif lbl == 0xFF:
                i += 12
            # Skip sub-blocks
            while i < n:
                sz = data[i]
                i += 1
                if sz == 0:
                    break
                i += sz
        elif b == 0x3B:
            break
        else:
            i += 1
    
    filesize = os.path.getsize(GIF)
    
    result = f"""=== GIF 基本信息 ===
文件名: QQ20260531-013327-HD.gif
尺寸: {w} x {h}
格式: {hdr}
帧数: {frames}
文件大小: {filesize:,} bytes ({filesize/1024:.1f} KB / {filesize/1048576:.2f} MB)
"""
    
    with open(OUT, 'w', encoding='utf-8') as fw:
        fw.write(result)
    print('OK - saved to _gif_data.txt')
    
except Exception as e:
    with open(OUT, 'w', encoding='utf-8') as fw:
        fw.write(f'ERROR: {e}')
    print(f'ERROR: {e}')