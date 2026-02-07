# -*- coding: utf-8 -*-
"""
从 wiiu_games.json、wiiutdb.xml、gametitle_wiiu.txt 提取并合并数据，
生成包含 title_id、game_id、游戏中文名、游戏英文名 的 JSON 文件。
"""
import json
import os
import xml.etree.ElementTree as ET

# 数据文件路径（相对本脚本所在目录）
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
WIIU_GAMES_JSON = os.path.join(SCRIPT_DIR, "wiiu_games.json")
WIIUTDB_XML = os.path.join(SCRIPT_DIR, "wiiutdb.xml")
GAMETITLE_WIIU_TXT = os.path.join(SCRIPT_DIR, "gametitle_wiiu.txt")
OUTPUT_JSON = os.path.join(SCRIPT_DIR, "wiiu_titles.json")


def load_gametitle_wiiu(path):
    """gametitle_wiiu.txt: 每行 "GAMEID = 中文名"，返回 dict game_id -> chinese_name"""
    result = {}
    if not os.path.isfile(path):
        return result
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or "=" not in line or line.startswith("TITLES"):
                continue
            idx = line.index("=")
            game_id = line[:idx].strip()
            chinese_name = line[idx + 1 :].strip()
            if game_id:
                result[game_id] = chinese_name
    return result


def load_wiiutdb_titles(path):
    """wiiutdb.xml: 每个 <game> 有 <id> 和 <locale lang='EN'><title>，返回 dict game_id -> english_name"""
    result = {}
    if not os.path.isfile(path):
        return result
    tree = ET.parse(path)
    root = tree.getroot()
    for game in root.findall(".//game"):
        eid = game.find("id")
        if eid is None or not (eid.text or "").strip():
            continue
        game_id = eid.text.strip()
        en_title = None
        for loc in game.findall("locale"):
            if (loc.get("lang") or "").upper() == "EN":
                t = loc.find("title")
                if t is not None and (t.text or "").strip():
                    en_title = t.text.strip()
                    break
        if en_title:
            result[game_id] = en_title
    return result


def resolve_game_id(product_code, gametitle_ids, wiiutdb_ids):
    """根据 ProductCode（4 位）解析出 6 位 game_id，优先 6 位"""
    pc = (product_code or "").strip().upper()
    if len(pc) < 4:
        return None
    candidates = []
    for gid in gametitle_ids:
        if gid.upper().startswith(pc):
            candidates.append(gid)
    for gid in wiiutdb_ids:
        if gid.upper().startswith(pc) and gid not in candidates:
            candidates.append(gid)
    if not candidates:
        return None
    # 优先 6 位
    six_char = [c for c in candidates if len(c) == 6]
    if six_char:
        return six_char[0]
    four_char = [c for c in candidates if len(c) == 4]
    if four_char:
        return four_char[0]
    return candidates[0]


def main():
    print("加载 gametitle_wiiu.txt ...")
    chinese_by_id = load_gametitle_wiiu(GAMETITLE_WIIU_TXT)
    print("加载 wiiutdb.xml ...")
    english_by_id = load_wiiutdb_titles(WIIUTDB_XML)
    print("加载 wiiu_games.json ...")
    if not os.path.isfile(WIIU_GAMES_JSON):
        print("未找到 wiiu_games.json，仅从 gametitle + wiiutdb 生成（无 title_id）")
        # 仅从 gametitle + wiiutdb 生成：以 game_id 为主键
        out_list = []
        seen = set()
        for gid in sorted(chinese_by_id.keys() | english_by_id.keys()):
            if gid in seen:
                continue
            seen.add(gid)
            out_list.append({
                "title_id": "",
                "game_id": gid,
                "chinese_name": chinese_by_id.get(gid, ""),
                "english_name": english_by_id.get(gid, ""),
            })
        with open(OUTPUT_JSON, "w", encoding="utf-8") as f:
            json.dump(out_list, f, ensure_ascii=False, indent=2)
        print("已生成:", OUTPUT_JSON, "共", len(out_list), "条")
        return

    with open(WIIU_GAMES_JSON, "r", encoding="utf-8") as f:
        games_list = json.load(f)

    out_list = []
    for item in games_list:
        title_id = (item.get("TitleId") or "").strip()
        product_code = (item.get("ProductCode") or "").strip()
        eshop_name = (item.get("Name") or "").strip()
        if not title_id:
            continue
        game_id = resolve_game_id(
            product_code,
            list(chinese_by_id.keys()),
            list(english_by_id.keys()),
        )
        if not game_id:
            game_id = product_code
        chinese_name = chinese_by_id.get(game_id) or chinese_by_id.get(product_code) or ""
        english_name = english_by_id.get(game_id) or english_by_id.get(product_code) or eshop_name
        out_list.append({
            "title_id": title_id,
            "game_id": game_id,
            "chinese_name": chinese_name,
            "english_name": english_name,
        })

    with open(OUTPUT_JSON, "w", encoding="utf-8") as f:
        json.dump(out_list, f, ensure_ascii=False, indent=2)

    print("已生成:", OUTPUT_JSON)
    print("共", len(out_list), "条记录（title_id + game_id + 中文名 + 英文名）")


if __name__ == "__main__":
    main()
