#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
扫描当前目录下的图片，根据图片名称第一个汉字的拼音首字母重命名。
例如：魔界村.zip.png → M魔界村.zip.png
"""

import os
import re
from pypinyin import pinyin, Style


def get_first_chinese_pinyin_initial(text: str) -> str:
    """获取字符串中第一个汉字的拼音首字母（大写）"""
    for char in text:
        if '\u4e00' <= char <= '\u9fff':  # 检查是否为汉字
            py = pinyin(char, style=Style.FIRST_LETTER)[0][0]
            return py.upper()
    return ""


def rename_images_in_directory(directory: str = None):
    """重命名目录下的图片文件"""
    if directory is None:
        directory = os.path.dirname(os.path.abspath(__file__))
    
    if not os.path.isdir(directory):
        print(f"目录不存在: {directory}")
        return
    
    # 匹配 .png 文件（可能有多个扩展名，如 .zip.png）
    pattern = re.compile(r'^(.+)\.png$', re.IGNORECASE)
    
    renamed_count = 0
    skipped_count = 0
    
    files = sorted(os.listdir(directory))
    
    for filename in files:
        if not filename.lower().endswith('.png'):
            continue
        
        match = pattern.match(filename)
        if not match:
            continue
        
        base = match.group(1)  # 去掉 .png 后的部分，如 "魔界村.zip"
        
        # 检查是否已以大写字母开头
        if base and base[0].isupper() and base[0].isalpha():
            skipped_count += 1
            print(f"跳过（已大写开头）: {filename}")
            continue
        
        # 获取第一个汉字的拼音首字母
        initial = get_first_chinese_pinyin_initial(base)
        if not initial:
            skipped_count += 1
            print(f"跳过（无汉字）: {filename}")
            continue
        
        new_name = f"{initial}{base}.png"
        old_path = os.path.join(directory, filename)
        new_path = os.path.join(directory, new_name)
        
        # 检查目标文件名是否已存在
        if os.path.exists(new_path):
            print(f"跳过（目标已存在）: {filename} → {new_name}")
            skipped_count += 1
            continue
        
        try:
            os.rename(old_path, new_path)
            print(f"重命名: {filename} → {new_name}")
            renamed_count += 1
        except Exception as e:
            print(f"重命名失败: {filename} - {e}")
    
    print(f"\n完成！重命名: {renamed_count} 个，跳过: {skipped_count} 个")


if __name__ == "__main__":
    print("扫描目录:", os.path.dirname(os.path.abspath(__file__)))
    rename_images_in_directory()
