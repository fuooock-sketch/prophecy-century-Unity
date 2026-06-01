# Write GIF info to file directly, no shell redirect needed
import struct
import os

gif = r'd:\Prophecy-Century\GameProject\prophecy_century\prophecy-century-Unity\tools\QQ20260531-013327-HD.gif'
out = r'd:\Prophecy-Century\GameProject\prophecy_century\prophecy-century-Unity\tools\gif_info_v2.txt'

result_lines = []

try:
    with open(gif, 'rb') as f:
        header = f.read(6)
        version = header[3:6].decode('ascii')
        result_lines.append(f'Version: {version}')

        lsd = f.read(7)
        width, height = struct.unpack('<HH', lsd[:4])
        packed = lsd[4]
        has_gct = (packed & 0x80) != 0
        color_res = ((packed >> 4) & 0x07) + 1
        gct_size = 2 ** ((packed & 0x07) + 1)
        result_lines.append(f'Dimensions: {width} x {height}')
        result_lines.append(f'Global Color Table: {has_gct}')
        result_lines.append(f'Color Resolution: {color_res} bits')
        result_lines.append(f'GCT Size: {gct_size}')

        if has_gct:
            f.read(gct_size * 3)

        frame_count = 0
        while True:
            bt_b = f.read(1)
            if not bt_b:
                break
            bt = bt_b[0]
            if bt == 0x21:
                label = f.read(1)[0]
                if label == 0xF9:
                    gce = f.read(6)
                    delay = struct.unpack('<H', gce[2:4])[0]
                    while True:
                        sz = f.read(1)[0]
                        if sz == 0:
                            break
                        f.read(sz)
                elif label in (0xFE, 0xFF, 0x01):
                    skip = 12 if label == 0xFF else (13 if label == 0x01 else 0)
                    f.read(skip)
                    while True:
                        sz = f.read(1)[0]
                        if sz == 0:
                            break
                        f.read(sz)
            elif bt == 0x2C:
                frame_count += 1
                f.read(9)
                code = f.read(1)[0]
                while True:
                    sz = f.read(1)[0]
                    if sz == 0:
                        break
                    f.read(sz)
            elif bt == 0x3B:
                break

        result_lines.append(f'Frames: {frame_count}')

        fsize = os.path.getsize(gif)
        result_lines.append(f'File Size: {fsize:,} bytes ({fsize/1024:.1f} KB)')

    with open(out, 'w', encoding='utf-8') as fw:
        fw.write('\n'.join(result_lines))
    print('DONE')

except Exception as e:
    with open(out, 'w', encoding='utf-8') as fw:
        fw.write(f'ERROR: {e}')
    print(f'ERROR: {e}')