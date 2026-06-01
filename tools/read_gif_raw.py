"""Read GIF basic info without PIL - pure binary parsing"""
import struct
import sys

gif_path = r'd:\Prophecy-Century\GameProject\prophecy_century\prophecy-century-Unity\tools\QQ20260531-013327-HD.gif'

try:
    with open(gif_path, 'rb') as f:
        # Read header: 6 bytes
        header = f.read(6)
        if header[:3] not in (b'GIF',):
            raise ValueError('Not a valid GIF file')
        
        version = header[3:6].decode('ascii')
        
        # Read logical screen descriptor: 7 bytes
        # width (2), height (2), packed (1), bg color (1), aspect (1)
        lsd = f.read(7)
        width, height = struct.unpack('<HH', lsd[:4])
        packed = lsd[4]
        
        has_global_color_table = (packed & 0x80) != 0
        color_resolution = ((packed >> 4) & 0x07) + 1
        sort_flag = (packed >> 3) & 0x01
        global_color_table_size = 2 ** ((packed & 0x07) + 1)
        bg_color_index = lsd[5]
        pixel_aspect = lsd[6]
        
        # Read global color table if present
        if has_global_color_table:
            gct = f.read(global_color_table_size * 3)
        
        # Count frames by scanning blocks
        frame_count = 0
        while True:
            block_type = f.read(1)
            if not block_type:
                break
            
            bt = block_type[0]
            
            if bt == 0x21:  # Extension
                ext_label = f.read(1)
                if not ext_label:
                    break
                label = ext_label[0]
                if label == 0xF9:  # Graphic Control Extension
                    gce = f.read(6)
                    delay = struct.unpack('<H', gce[2:4])[0]
                    # consume data sub-blocks
                    while True:
                        size = f.read(1)
                        if not size:
                            break
                        sz = size[0]
                        if sz == 0:
                            break
                        f.read(sz)
                elif label == 0xFE:  # Comment Extension
                    while True:
                        size = f.read(1)
                        if not size:
                            break
                        sz = size[0]
                        if sz == 0:
                            break
                        f.read(sz)
                elif label == 0xFF:  # Application Extension
                    f.read(12)  # skip app id + auth code
                    while True:
                        size = f.read(1)
                        if not size:
                            break
                        sz = size[0]
                        if sz == 0:
                            break
                        f.read(sz)
                elif label == 0x01:  # Plain Text Extension
                    f.read(13)
                    while True:
                        size = f.read(1)
                        if not size:
                            break
                        sz = size[0]
                        if sz == 0:
                            break
                        f.read(sz)
            
            elif bt == 0x2C:  # Image Descriptor
                frame_count += 1
                f.read(9)  # left, top, width, height, packed
                # Local color table if present
                # Skip over image data
                code_size = f.read(1)[0]
                # Read LZW sub-blocks
                while True:
                    size = f.read(1)
                    if not size:
                        break
                    sz = size[0]
                    if sz == 0:
                        break
                    f.read(sz)
            
            elif bt == 0x3B:  # Trailer
                break
        
        filesize = 0
        with open(gif_path, 'rb') as f2:
            f2.seek(0, 2)
            filesize = f2.tell()
        
        output = f"""GIF File: QQ20260531-013327-HD.gif
Version: {version}
Dimensions: {width} x {height}
Frame Count: {frame_count}
File Size: {filesize:,} bytes ({filesize/1024:.1f} KB / {filesize/1048576:.2f} MB)
Global Color Table: {'Yes' if has_global_color_table else 'No'}
Color Resolution: {color_resolution} bits
Pixel Aspect Ratio: {pixel_aspect}
"""
        print(output)
        
except Exception as e:
    print(f'Error: {e}')
    import traceback
    traceback.print_exc()