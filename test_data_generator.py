import json
import random
from datetime import datetime, timedelta, timezone
import os
import re

def parse_weighted_choices(options, weights_str):
    weights = [int(x) for x in weights_str.split(":")]
    result = []
    for opt, w in zip(options, weights):
        result.extend([opt] * w)
    return result

def parse_frequency(freq_str, total_count):
    """
    支持格式：
    1/3^1   表示3±1天一次
    2^1/1   表示1天2±1次
    1/2     表示2天1次
    """
    freq_str = freq_str.replace(" ", "")
    # 1/3^1
    m = re.match(r"(\d+)(?:\^(\d+))?/(\d+)(?:\^(\d+))?", freq_str)
    if m:
        n = int(m.group(1))
        n_var = int(m.group(2)) if m.group(2) else 0
        d = int(m.group(3))
        d_var = int(m.group(4)) if m.group(4) else 0
        # n/d 表示d天n次
        # n_var, d_var 表示波动
        dates = []
        now = datetime.now(timezone(timedelta(hours=8)))
        i = 0
        while len(dates) < total_count:
            # 随机天数
            days = d + random.randint(-d_var, d_var)
            days = max(1, days)
            # 随机次数
            times = n + random.randint(-n_var, n_var)
            times = max(1, times)
            for _ in range(times):
                if len(dates) < total_count:
                    dt = now - timedelta(days=i)
                    dates.append(dt)
            i += days
        return dates[:total_count]
    # 2^1/1
    m = re.match(r"(\d+)\^(\d+)/(\d+)", freq_str)
    if m:
        n = int(m.group(1))
        n_var = int(m.group(2))
        d = int(m.group(3))
        dates = []
        now = datetime.now(timezone(timedelta(hours=8)))
        i = 0
        while len(dates) < total_count:
            times = n + random.randint(-n_var, n_var)
            times = max(1, times)
            for _ in range(times):
                if len(dates) < total_count:
                    dt = now - timedelta(days=i)
                    dates.append(dt)
            i += d
        return dates[:total_count]
    # 1/2
    m = re.match(r"(\d+)/(\d+)", freq_str)
    if m:
        n = int(m.group(1))
        d = int(m.group(2))
        dates = []
        now = datetime.now(timezone(timedelta(hours=8)))
        i = 0
        while len(dates) < total_count:
            for _ in range(n):
                if len(dates) < total_count:
                    dt = now - timedelta(days=i)
                    dates.append(dt)
            i += d
        return dates[:total_count]
    # 默认：每天一条
    now = datetime.now(timezone(timedelta(hours=8)))
    return [now - timedelta(days=i) for i in range(total_count)]

def random_record(i, min_minute, max_minute, min_second, max_second, locations, tools, moods, date, watched_prob, climax_prob):
    now = datetime.now(timezone(timedelta(hours=8)))
    date = date.isoformat(timespec='microseconds')
    minute = random.randint(min_minute, max_minute)
    second = random.randint(min_second, max_second)
    duration_td = timedelta(minutes=minute, seconds=second)
    hours = duration_td.seconds // 3600
    minutes = (duration_td.seconds % 3600) // 60
    seconds = duration_td.seconds % 60
    duration = f"{hours:02}:{minutes:02}:{seconds:02}"
    detail = {
        "Remark": f"测试备注{i}",
        "Location": random.choice(locations),
        "WatchedMovie": random.random() < watched_prob,
        "Climax": random.random() < climax_prob,
        "Tool": random.choice(tools),
        "Score": round(random.uniform(0, 5), 1),
        "Mood": random.choice(moods)
    }
    return {
        "Date": date,
        "Duration": duration,
        "Detail": detail
    }

def main():
    try:
        count = int(input("请输入要生成的数据条数（默认50）：") or "50")
    except Exception:
        count = 50
    try:
        min_minute = int(input("请输入最小分钟数（默认2）：") or "2")
    except Exception:
        min_minute = 2
    try:
        max_minute = int(input("请输入最大分钟数（默认10）：") or "10")
    except Exception:
        max_minute = 10
    try:
        min_second = int(input("请输入最小秒数（默认0）：") or "0")
    except Exception:
        min_second = 0
    try:
        max_second = int(input("请输入最大秒数（默认59）：") or "59")
    except Exception:
        max_second = 59

    location_opts = ["卧室", "浴室", "客厅"]
    tool_opts = ["手", "飞机杯", "娃娃"]
    mood_opts = ["平静", "愉悦", "兴奋", "疲惫", "这是最后一次！"]

    location_weights = input(f"请输入地点权重（用:分隔，{len(location_opts)}项，默认2:2:1）：") or "2:2:1"
    tool_weights = input(f"请输入道具权重（用:分隔，{len(tool_opts)}项，默认2:3:1）：") or "2:3:1"
    mood_weights = input(f"请输入心情权重（用:分隔，{len(mood_opts)}项，默认2:3:3:2:1）：") or "2:3:3:2:1"

    locations = parse_weighted_choices(location_opts, location_weights)
    tools = parse_weighted_choices(tool_opts, tool_weights)
    moods = parse_weighted_choices(mood_opts, mood_weights)

    watched_prob = input("请输入“观看小电影”概率（0~1，默认0.5）：") or "0.5"
    climax_prob = input("请输入“高潮”概率（0~1，默认0.8）：") or "0.8"
    try:
        watched_prob = float(watched_prob)
    except Exception:
        watched_prob = 0.5
    try:
        climax_prob = float(climax_prob)
    except Exception:
        climax_prob = 0.8

    freq = input("请输入频率（如1/3^1表示3±1天一次，2^1/1表示1天2±1次，1/2表示2天1次，默认1/3^1）：") or "1/3^1"
    dates = parse_frequency(freq, count)
    records = [
        random_record(
            i, min_minute, max_minute, min_second, max_second,
            locations, tools, moods, dates[i], watched_prob, climax_prob
        )
        for i in range(count)
    ]
    base_dir = os.path.expandvars(r"%LOCALAPPDATA%\DickHelper")
    os.makedirs(base_dir, exist_ok=True)
    file_path = f"{base_dir}/history.json"
    with open(file_path, "w", encoding="utf-8") as f:
        json.dump(records, f, ensure_ascii=False, indent=2)
    print(f"测试数据已写入: {file_path}")

if __name__ == "__main__":
    main()
