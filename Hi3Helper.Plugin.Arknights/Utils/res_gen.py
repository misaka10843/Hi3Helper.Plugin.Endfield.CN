import os
import io
try:
    from PIL import Image
except ImportError:
    print("错误: 未检测到 Pillow 库。")
    print("请运行: pip install Pillow")
    exit(1)

def bytes_to_csharp_hex(data, indent_level=8):
    hex_items = [f"0x{byte:02x}" for byte in data]
    
    # 每行 16 个字节
    items_per_line = 16
    lines = []
    
    for i in range(0, len(hex_items), items_per_line):
        chunk = hex_items[i:i + items_per_line]
        line_str = " " * indent_level + ", ".join(chunk)
        lines.append(line_str)

    return ",\n".join(lines)

def process_icon(input_path, output_ico_path):
    if not os.path.exists(input_path):
        print(f"Warning: Icon文件未找到 - {input_path}")
        return b""

    try:
        with Image.open(input_path) as img:
            print(f"正在生成 ICO 文件 (256x256)...")
            ico_img = img.resize((256, 256), Image.Resampling.LANCZOS)
            ico_img.save(output_ico_path, format='ICO')
            print(f"ICO 已保存: {output_ico_path}")
            
            with open(input_path, 'rb') as f:
                return f.read()
    except Exception as e:
        print(f"处理 Icon 出错: {e}")
        return b""

def process_poster(input_path, quality=70, max_width=1080):
    if not os.path.exists(input_path):
        print(f"Warning: Poster文件未找到 - {input_path}")
        return b""

    try:
        with Image.open(input_path) as img:
            if img.mode in ("RGBA", "P"):
                img = img.convert("RGB")

            if img.width > max_width:
                ratio = max_width / float(img.width)
                new_height = int(float(img.height) * ratio)
                img = img.resize((max_width, new_height), Image.Resampling.LANCZOS)
                print(f"海报尺寸已调整为: {max_width}x{new_height}")

            buffer = io.BytesIO()
            img.save(buffer, format="JPEG", quality=quality)
            compressed_data = buffer.getvalue()
            
            original_size = os.path.getsize(input_path)
            compressed_size = len(compressed_data)
            print(f"海报压缩完成: {original_size/1024:.2f}KB -> {compressed_size/1024:.2f}KB (压缩率: {compressed_size/original_size:.0%})")
            
            return compressed_data

    except Exception as e:
        print(f"处理 Poster 出错: {e}")
        return b""

def generate_arknights_code(icon_path, poster_path, output_file, output_ico_name):
    print("=== 开始处理 ===")

    icon_bytes = process_icon(icon_path, output_ico_name)
    icon_data_str = bytes_to_csharp_hex(icon_bytes)
    
    print("-" * 30)

    poster_bytes = process_poster(poster_path, quality=70)
    poster_data_str = bytes_to_csharp_hex(poster_bytes)

    print("-" * 30)
    print("正在构建 C# 代码...")

    csharp_code = f"""using System;
using System.Collections.Generic;
using System.Text;

namespace Hi3Helper.Plugin.Arknights.Utils;
public static partial class ArknightsImageData
{{
    internal static readonly byte[] ArknightsAppIconData = 
    [
{icon_data_str}
    ];

    internal static readonly byte[] ArknightsPosterData =
    [
{poster_data_str}
    ];
}}"""

    # 写入文件
    try:
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(csharp_code)
        print(f"\n成功！代码已生成到: {output_file}")
    except Exception as e:
        print(f"写入文件时出错: {e}")

if __name__ == "__main__":
    input_icon_path = "icon.png"
    input_poster_path = "poster.png"
    
    output_cs_path = "ArknightsImageData.cs"
    output_ico_path = "icon.ico"

    generate_arknights_code(input_icon_path, input_poster_path, output_cs_path, output_ico_path)