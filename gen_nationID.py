import os
import json
from collections import OrderedDict

# 指定的目录
directory1 = r"C:\FModel\Output\Exports\WRC\Content\WRC\Data\Nationality"
directory2 = r"C:\Projects\Simhub-Plugin-RacenetReceiver\icons\flags"

# 创建一个空字典来存储文件名
file_dict = {}

# 遍历第一个目录中的所有文件
for filename in os.listdir(directory1):
    # 提取id和国家名
    id_and_country = filename.replace('CDA_Nationality_', '').replace('.json', '')
    id, country = id_and_country.split('_', 1)
    country = country.replace('_', '')  # 去掉下划线

    # 遍历第二个目录中的所有文件
    for flag_filename in os.listdir(directory2):
        # 如果国家名在flag文件名中
        if country in flag_filename:
            # 使用id作为键，文件名作为值
            file_dict[int(id)] = flag_filename
            break

# 将字典按键（也就是id）排序
ordered_dict = OrderedDict(sorted(file_dict.items()))

# 将字典写入json文件
with open('output.json', 'w') as f:
    json.dump(ordered_dict, f, ensure_ascii=False, indent=4)