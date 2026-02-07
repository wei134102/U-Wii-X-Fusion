# -*- coding: utf-8 -*-
"""根据 wiiutdb.txt 生成 gametitle_wiiu.txt，翻译游戏名为中文并保留格式：ID = 中文名 (区)"""

import re
import os

# 区域码（ID 第5位，6位ID；或第4位，4位ID）-> 中文后缀
REGION_MAP = {
    'E': '美', 'J': '日', 'P': '欧', 'D': '欧', 'K': '韩',
    'I': '意', 'F': '法', 'S': '西', 'N': '韩', 'A': '亚',
    'Z': '欧', 'C': '中', 'U': '美', 'V': '欧', 'W': '欧',
    'X': '欧', 'Y': '欧', 'R': '欧', 'Q': '欧', 'M': '美',
    'L': '欧', 'H': '欧', 'G': '欧', 'B': '欧', 'T': '台',
}

# 英文/日文游戏名 -> 中文名（部分常见游戏，其余保留原名+区）
TITLE_ZH = {
    # 第一方 / 知名
    "Bayonetta": "猎天使魔女",
    "Bayonetta 2": "猎天使魔女2",
    "Animal Crossing: Amiibo Festival": "动物之森：Amiibo节日",
    "Doubutsu no Mori: Amiibo Festival": "动物之森：Amiibo节日",
    "Mario Party 10": "马里奥派对 10",
    "Pikmin 3": "皮克敏3",
    "The Wonderful 101": "神奇101",
    "Resident Evil: Revelations": "生化危机：启示录",
    "Biohazard: Revelations: Unveiled Edition": "生化危机：启示录 揭开版",
    "Batman: Arkham City: Armored Edition": "蝙蝠侠：阿卡姆之城 装甲版",
    "Batman: Arkham City: Armoured Edition": "蝙蝠侠：阿卡姆之城 装甲版",
    "Batman: Arkham Origins": "蝙蝠侠：阿卡姆起源",
    "Mario & Sonic at the Rio 2016 Olympic Games": "马里奥与索尼克 里约2016奥运会",
    "Mario & Sonic at Rio Olympic": "马里奥与索尼克 里约奥运会",
    "Mario & Sonic at the Sochi 2014 Olympic Winter Games": "马里奥与索尼克 索契2014冬奥会",
    "Mario & Sonic at the Sochi Olympic": "马里奥与索尼克 索契冬奥会",
    "Call of Duty: Ghosts": "使命召唤：幽灵",
    "Call of Duty: Black Ops II": "使命召唤：黑色行动2",
    "Devil's Third": "恶魔三全音",
    "Dragon Quest X: Nemureru Yuusha to Michibiki no Meiyuu Online": "勇者斗恶龙X 觉醒的勇者与引导的盟约 在线",
    "Dragon Quest X: 5000-nen no Tabiji: Haruka naru Kokyou e Online": "勇者斗恶龙X 5000年的旅程 迈向遥远故乡 在线",
    "Disney Infinity": "迪士尼无限",
    "Disney Infinity 2.0 Edition": "迪士尼无限2.0",
    "Disney Infinity 2.0: Play Without Limits": "迪士尼无限2.0 畅玩无界",
    "Disney Infinity 3.0 Edition": "迪士尼无限3.0",
    "Disney Infinity 3.0": "迪士尼无限3.0",
    "Disney Infinity 3.0: Play Without Limits": "迪士尼无限3.0 畅玩无界",
    "Deus Ex: Human Revolution: Director's Cut": "杀出重围：人类革命 导演剪辑版",
    "NES Remix Pack": "NES 重混合集",
    "Famicom Remix 1 + 2": "红白机重混 1+2",
    "Star Fox Zero": "星际火狐 零",
    "Star Fox Guard": "星际火狐 守卫",
    "Monster Hunter 3 Ultimate": "怪物猎人3 终极版",
    "Monster Hunter 3G: HD Ver.": "怪物猎人3G HD版",
    "Splatoon": "喷射战士",
    "Captain Toad: Treasure Tracker": "前进！奇诺比奥队长",
    "Susume! Kinopio Taichou": "前进！奇诺比奥队长",
    "Tekken Tag Tournament 2: Wii U Edition": "铁拳 标签锦标赛2 Wii U版",
    "Zero: Nuregarasu no Miko": "零：濡鸦之巫女",
    "Project Zero: Maiden of Black Water": "零：濡鸦之巫女",
    "The LEGO Movie: Videogame": "乐高电影 游戏版",
    "LEGO Movie: The Game": "乐高电影 游戏",
    "Nintendo Land": "任天堂乐园",
    "LEGO The Hobbit": "乐高霍比特人",
    "LEGO Jurassic World": "乐高侏罗纪世界",
    "LEGO Marvel Super Heroes": "乐高漫威超级英雄",
    "LEGO Marvel Super Heroes: The Game": "乐高漫威超级英雄",
    "LEGO Marvel Avengers": "乐高漫威复仇者",
    "The Legend of Zelda: Breath of the Wild": "塞尔达传说：旷野之息",
    "Zelda no Densetsu: Breath of the Wild": "塞尔达传说：旷野之息",
    "Super Mario Maker": "超级马里奥制造",
    "Mario Kart 8": "马里奥赛车8",
    "Mass Effect 3: Special Edition": "质量效应3 特别版",
    "Mass Effect 3: Tokubetsu-ban": "质量效应3 特别版",
    "The Amazing Spider-Man 2": "超凡蜘蛛侠2",
    "The Amazing Spider-Man: Ultimate Edition": "超凡蜘蛛侠 终极版",
    "Ninja Gaiden 3: Razor's Edge": "忍者龙剑传3 刀锋边缘",
    "LEGO City Undercover": "乐高都市 卧底风云",
    "Pokken Tournament": "宝可梦铁拳",
    "Pokken: Pokken Tournament": "宝可梦铁拳",
    "Wii Party U": "Wii 派对 U",
    "Disney Planes": "飞机总动员",
    "Pac-Man and the Ghostly Adventures": "吃豆人 鬼灵精怪大冒险",
    "Pac-Man and the Ghostly Adventures 2": "吃豆人 鬼灵精怪大冒险2",
    "LEGO Dimensions": "乐高次元",
    "Rabbids Land": "疯狂兔子 大陆",
    "Super Mario 3D World": "超级马里奥3D世界",
    "Donkey Kong Country: Tropical Freeze": "大金刚国度 热带寒流",
    "Donkey Kong: Tropical Freeze": "大金刚 热带寒流",
    "Rayman Legends": "雷曼传奇",
    "New Super Mario Bros. U": "新超级马里奥兄弟 U",
    "New Super Luigi U": "新超级路易吉 U",
    "New Super Mario Bros. U + New Super Luigi U": "新超级马里奥兄弟 U + 新超级路易吉 U",
    "Ryuu ga Gotoku 1 & 2 HD for Wii U": "如龙1&2 HD for Wii U",
    "Sonic & All-Stars Racing Transformed": "索尼克与全明星赛车 变形",
    "Scribblenauts Unmasked: A DC Comics Adventure": "涂鸦冒险家  DC漫画大冒险",
    "Scribblenauts Unlimited": "涂鸦冒险家 无限",
    "Tokyo Mirage Sessions FE": "幻影异闻录#FE",
    "Gen'ei Ibunroku FE": "幻影异闻录#FE",
    "Wii Fit U": "Wii Fit U",
    "Sing Party": "欢唱派对",
    "Minecraft: Wii U Edition": "我的世界 Wii U版",
    "One Piece: Unlimited World R": "海贼王 无尽世界R",
    "One Piece: Unlimited World Red": "海贼王 无尽世界R",
    "Mario Tennis: Ultra Smash": "马里奥网球 终极扣杀",
    "Xenoblade Chronicles X": "异度之刃X",
    "XenobladeX": "异度之刃X",
    "Super Smash Bros. for Wii U": "任天堂明星大乱斗 for Wii U",
    "Dairantou Smash Brothers for Wii U": "任天堂明星大乱斗 for Wii U",
    "Kirby and the Rainbow Curse": "星之卡比 机械星球",
    "Touch! Kirby Super Rainbow": "触摸！卡比 超级彩虹",
    "Kirby and the Rainbow Paintbrush": "星之卡比 机械星球",
    "Yoshi's Woolly World": "毛线耀西",
    "Yoshi Wool World": "毛线耀西",
    "The Legend of Zelda: Twilight Princess HD": "塞尔达传说 黄昏公主 HD",
    "Zelda no Densetsu: Twilight Princess HD": "塞尔达传说 黄昏公主 HD",
    "ZombiU": "僵尸U",
    "The Legend of Zelda: The Wind Waker HD": "塞尔达传说 风之杖 HD",
    "Zelda no Densetsu: Kaze no Takt HD": "塞尔达传说 风之杖 HD",
    "Hyrule Warriors": "塞尔达无双",
    "Zelda Musou": "塞尔达无双",
    "Paper Mario: Color Splash": "纸片马里奥 色彩飞溅",
    "Just Dance 2014": "舞力全开2014",
    "Just Dance Wii U": "舞力全开 Wii U",
    "Just Dance 2016": "舞力全开2016",
    "Just Dance 4": "舞力全开4",
    "Just Dance Kids 2014": "舞力全开 儿童版2014",
    "Just Dance 2015": "舞力全开2015",
    "Just Dance 2017": "舞力全开2017",
    "Just Dance 2018": "舞力全开2018",
    "Just Dance 2019": "舞力全开2019",
    "Game & Wario": "Game & Wario",
    "Assassin's Creed IV: Black Flag": "刺客信条4 黑旗",
    "Assassin's Creed III": "刺客信条3",
    "Tom Clancy's Splinter Cell: Blacklist": "细胞分裂 黑名单",
    "Watch_Dogs": "看门狗",
    "Watch Dogs": "看门狗",
    "Wii Sports Club": "Wii 运动 俱乐部",
    "Injustice: Gods Among Us": "不义联盟 人间之神",
    "Injustice: Hero no Gekitotsu": "不义联盟 人间之神",
    "Darksiders II": "暗黑血统2",
    "Darksiders: Warmastered Edition": "暗黑血统 战神版",
    "Shovel Knight": "铲子骑士",
    "Art Academy: Atelier": "绘画教室 画室",
    "Jikkuri Egokoro Kyoushitsu": "绘画教室 画室",
    "DuckTales: Remastered": "唐老鸭历险记 重制版",
    "Mario vs. Donkey Kong: Minna de Mini Land": "马里奥vs大金刚 大家的迷你乐园",
    "Mario vs. Donkey Kong: Tipping Stars": "马里奥vs大金刚 迷你王国",
    "Wii Karaoke U": "Wii 卡拉OK U",
    "Dragon Quest X: Inishie no Ryuu no Denshou Online": "勇者斗恶龙X 远古之龙的传承 在线",
    "Dragon Quest X: All in One Package": "勇者斗恶龙X 合辑",
    "Dragon Quest X: All in One Package Version 1-Version 4": "勇者斗恶龙X 合辑 v1-v4",
    "Puyo Puyo Tetris": "魔法气泡 俄罗斯方块",
    "Kamen Rider: Battride War II": "假面骑士 斗骑大战2",
    "Monster Hunter Frontier G5: Premium Package": "怪物猎人 边境G5 高级包",
    "Monster Hunter Frontier G6: Premium Package": "怪物猎人 边境G6 高级包",
    "Monster Hunter Frontier G7: Premium Package": "怪物猎人 边境G7 高级包",
    "Monster Hunter Frontier G8: Premium Package": "怪物猎人 边境G8 高级包",
    "Monster Hunter Frontier G9: Premium Package": "怪物猎人 边境G9 高级包",
    "Monster Hunter Frontier G: Beginner's Package": "怪物猎人 边境G 入门包",
    "Monster Hunter Frontier GG: Premium Package": "怪物猎人 边境GG 高级包",
    "Monster Hunter Frontier G: Memorial Package": "怪物猎人 边境G 纪念包",
    "Warriors Orochi 3 Hyper": "无双大蛇2 终极版",
    "Musou Orochi 2 Hyper": "无双大蛇2 终极版",
    "Sangokushi 12": "三国志12",
    "Sangokushi 12 with Power-Up Kit": "三国志12 威力加强版",
    "Shin Hokuto Musou": "真·北斗无双",
    "LEGO Batman 2: DC Super Heroes": "乐高蝙蝠侠2 DC超级英雄",
    "LEGO Batman 3: Beyond Gotham": "乐高蝙蝠侠3 飞越哥谭",
    "LEGO Batman 3: The Game: Gotham kara Uchuu e": "乐高蝙蝠侠3 从哥谭到宇宙",
    "LEGO Star Wars: The Force Awakens": "乐高星球大战 原力觉醒",
    "LEGO Star Wars: Force no Kakusei": "乐高星球大战 原力觉醒",
    "Skylanders: Spyro's Adventure": "小龙斯派罗 冒险",
    "Skylanders: Giants": "小龙斯派罗 巨人",
    "Skylanders: Swap Force": "小龙斯派罗 交换力量",
    "Skylanders: Trap Team": "小龙斯派罗 陷阱团队",
    "Skylanders: SuperChargers": "小龙斯派罗 超级充能",
    "Skylanders: Imaginators": "小龙斯派罗 想象者",
    "Skylanders: Imaginators (Demo)": "小龙斯派罗 想象者（试玩）",
    "Ben 10: Omniverse": "少年骇客 全面进化",
    "Ben 10: Omniverse 2": "少年骇客 全面进化2",
    "Barbie Dreamhouse Party": "芭比梦幻屋派对",
    "Rapala Pro Bass Fishing": "拉帕拉 职业鲈鱼钓鱼",
    "Cabela's Dangerous Hunts 2013": "坎贝拉 危险狩猎2013",
    "Cabela's Big Game Hunter: Pro Hunts": "坎贝拉 大型猎物猎人 职业狩猎",
    "Cocoto Magic Circus 2": "科科托 魔法马戏团2",
    "The Croods: Prehistoric Party!": "疯狂原始人 史前派对",
    "Just Dance: Disney Party 2": "舞力全开 迪士尼派对2",
    "Disney Epic Mickey 2: The Power of Two": "迪士尼 史诗米奇2 双重力量",
    "Disney Epic Mickey 2: Futatsu no Chikara": "迪士尼 史诗米奇2 双重力量",
    "FIFA Soccer 13": "FIFA 足球13",
    "FIFA 13: World Class Soccer": "FIFA 13 世界级足球",
    "FIFA 13": "FIFA 13",
    "Fast & Furious: Showdown": "速度与激情 对决",
    "F1 Race Stars: Powered Up Edition": "F1 赛车明星 加强版",
    "Funky Barn": "放克农场",
    "Fit Music for Wii U": "Wii U 健身音乐",
    "Family Party: 30 Great Games: Obstacle Arcade": "家庭派对 30款游戏 障碍街机",
    "Simple Series for Wii U Vol. 1: The Family Party": "Wii U 简单系列 Vol.1 家庭派对",
    "Disney Planes: Fire & Rescue": "飞机总动员 火线救援",
    "Monster High: 13 Wishes": "精灵高中 13个愿望",
    "Rise of the Guardians": "守护者联盟",
    "Adventure Time: Explore the Dungeon Because I Don't Know!": "探险活宝 地牢探险",
    "Adventure Time: Explore the Dungeon Because I DON'T KNOW!": "探险活宝 地牢探险",
    "Adventure Time: Finn & Jake Investigations": "探险活宝 芬恩与杰克调查",
    "Adventure Time: Explore the Dungeon Because I Don't Know!": "探险活宝 地牢探险",
    "Shantae: Half-Genie Hero": "桑塔 半精灵英雄",
    "Hello Kitty Kruisers": "凯蒂猫 赛车",
    "Hot Wheels: World's Best Driver": "风火轮 世界最佳车手",
    "SteamWorld Collection": "蒸汽世界 合集",
    "Captain Toad: Treasure Tracker": "前进！奇诺比奥队长",
    "LEGO The Hobbit": "乐高霍比特人",
    "Luv Me Buddies: Wonderland": "爱我伙伴 仙境",
    "Madden NFL 13": "麦登NFL 13",
    "Mighty No. 9": "麦提9号",
    "Marvel Avengers: Battle for Earth": "漫威复仇者 地球之战",
    "NBA 2K13": "NBA 2K13",
    "Angry Birds Trilogy": "愤怒的小鸟 三部曲",
    "Angry Birds Star Wars": "愤怒的小鸟 星球大战",
    "Need for Speed: Most Wanted U: A Criterion Game": "极品飞车 最高通缉 U",
    "Disney Planes": "飞机总动员",
    "Phineas and Ferb: Quest for Cool Stuff": "飞哥与小佛 酷玩意大冒险",
    "Penguins of Madagascar": "马达加斯加的企鹅",
    "ESPN Sports Connection": "ESPN 运动连接",
    "Sports Connection": "运动连接",
    "The Smurfs 2": "蓝精灵2",
    "007 Legends": "007 传奇",
    "Turbo: Super Stunt Squad": "涡轮 超级特技队",
    "How to Train Your Dragon 2": "驯龙高手2",
    "Tank! Tank! Tank!": "坦克！坦克！坦克！",
    "Transformers Prime: The Game": "变形金刚 领袖之证",
    "Transformers: Rise of the Dark Spark": "变形金刚 暗焰崛起",
    "Youkai Watch Dance: Just Dance Special Version": "妖怪手表 舞力全开 特别版",
    "The Voice: I Want You": "美国好声音 我要你",
    "Axiom Verge": "公理边缘",
    "ABC Wipeout 3": "ABC 极限体能王3",
    "ABC Wipeout: Create & Crash": "ABC 极限体能王 创造与撞击",
    "The Walking Dead: Survival Instinct": "行尸走肉 生存本能",
    "Wheel of Fortune": "幸运转轮",
    "Your Shape: Fitness Evolved 2013": "塑身 健身进化2013",
    "Cars 3: Driven to Win": "赛车总动员3 极速取胜",
    "Minecraft: Story Mode: A Telltale Games Series: The Complete Adventure": "我的世界 故事模式 完整冒险",
    "Legend of Kay: Anniversary": "凯传奇 周年纪念版",
    "Rodea the Sky Soldier": "天空机士罗迪亚",
    "Sonic Boom: Rise of Lyric": "索尼克 音爆 崛起",
    "Sonic Toon: Taiko no Hihou": "索尼克 音爆 崛起",
    "Taiko no Tatsujin: Wii U Version!": "太鼓达人 Wii U版",
    "Taiko no Tatsujin: Atsumete Tomodachi Daisakusen!": "太鼓达人 集合！好友大作战",
    "Taiko no Tatsujin: Tokumori!": "太鼓达人 特别篇",
    "Terraria": "泰拉瑞亚",
    "The Book of Unwritten Tales 2": "未传之书2",
    "Guitar Hero Live": "吉他英雄 现场",
    "Angry Birds Star Wars": "愤怒的小鸟 星球大战",
    "Angry Birds Trilogy": "愤怒的小鸟 三部曲",
    "Rodea the Sky Soldier": "天空机士罗迪亚",
    "Runbow: Deluxe Edition": "彩虹 豪华版",
    "Kung Fu Panda: Showdown of Legendary Legends": "功夫熊猫 传奇对决",
    "Monster High: New Ghoul in School": "精灵高中 新来的鬼怪",
    "Snoopy's Grand Adventure": "史努比 大冒险",
    "Barbie & Her Sisters: Puppy Rescue": "芭比与姐妹 小狗救援",
    "Kamen Rider Summonride!": "假面骑士 召唤骑乘",
    "Fujiko F. Fujio Characters Daishuugou! SF Dotabata Party!!": "藤子·F·不二雄角色大集合！SF 爆笑派对！！",
    "Gotouchi Tetsudou: Gotouchi Chara to Nippon Zenkoku no Tabi": "当地铁路 当地角色与日本全国之旅",
    "Finding Teddy II: Definitive Edition": "寻找泰迪2 决定版",
    "Teslagrad": "特斯拉学徒",
    "Giana Sisters: Twisted Dreams: Director's Cut": "吉娜姐妹 扭曲之梦 导演剪辑版",
    "Shakedown: Hawaii": "夏威夷 大劫案",
    "Mii Maker": "Mii 制作",
    "Wii U Chat": "Wii U 聊天",
    "Daily Log": "每日记录",
    "Health and Safety Information": "健康与安全信息",
    "Niconico": "niconico",
    "YouTube": "YouTube",
    "Netflix": "Netflix",
    "Hulu Plus": "Hulu Plus",
    "Amazon Instant Video": "亚马逊即时视频",
    "Amazon / LOVEFiLM": "亚马逊 / LOVEFiLM",
}

def get_region(game_id: str) -> str:
    """从游戏ID取区域：6位ID看第4位(如AAFE01的E)，4位ID看第4位(如AAFE的E)"""
    if len(game_id) >= 4:
        c = game_id[3].upper()  # 第4个字符：E=美 J=日 P=欧 D=欧
    else:
        return "其他"
    return REGION_MAP.get(c, "其他")

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    src = os.path.join(script_dir, "wiiutdb.txt")
    dst = os.path.join(script_dir, "gametitle_wiiu.txt")

    if not os.path.isfile(src):
        print("未找到 wiiutdb.txt")
        return

    lines = []
    with open(src, "r", encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n\r")
            if not line.strip():
                lines.append(line)
                continue
            # 首行：标题行，改为新文件说明
            if line.startswith("TITLES = "):
                lines.append("TITLES = https://www.gametdb.com (type: WiiU language: ZHCN gametitle_wiiu)")
                continue
            # 格式: ID = Title 或 ID= Title
            m = re.match(r"^([A-Z0-9]{4,6})\s*=\s*(.+)$", line)
            if not m:
                lines.append(line)
                continue
            game_id, title = m.group(1), m.group(2).strip()
            region = get_region(game_id)
            zh = TITLE_ZH.get(title)
            if zh is None:
                zh = title  # 未收录则保留原名
            out = "{} = {} ({})".format(game_id, zh, region)
            lines.append(out)

    with open(dst, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
        f.write("\n")

    print("已生成: {}".format(dst))
    print("共 {} 行".format(len(lines)))

if __name__ == "__main__":
    main()
