import requests
from tqdm import tqdm
import json
import time
import os
import re


class TerraHistoricus:

    def __init__(self):
        self.headers = {
            'Referer': 'https://terra-historicus.hypergryph.com/',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) '
                          'Chrome/100.0.4896.75 Safari/537.36 Edg/100.0.1185.36 '
        }

    def __get_comics_cid(self):
        response = requests.get(
            url='https://terra-historicus.hypergryph.com/api/comic',
            headers=self.headers
        )
        comic_list = json.loads(response.text)['data']
        for comic in comic_list:
            yield comic['cid']

    def __get_comic_info(self):
        for cid in self.__get_comics_cid():
            response = requests.get(
                url=f'https://terra-historicus.hypergryph.com/api/comic/{cid}',
                headers=self.headers
            )
            comic_info = json.loads(response.text)['data']
            yield comic_info

    def __get_comic_pages(self, parent_cid, cid):
        response = requests.get(
            url=f'https://terra-historicus.hypergryph.com/api/comic/{parent_cid}/episode/{cid}',
            headers=self.headers
        )
        return len(json.loads(response.text)['data']['pageInfos'])

    def __get_comic_data(self, parent_cid, cid, page_nums):
        for num in range(1, page_nums + 1):
            response = requests.get(
                url=f'https://terra-historicus.hypergryph.com/api/comic/{parent_cid}/episode/{cid}/page?pageNum={num}',
                headers=self.headers
            )
            yield json.loads(response.text)['data']['url']

    def save_data(self):
        path_detection = re.compile(r'[\\/:*?<>"|]')

        def is_path(path):
            if not os.path.exists(path):
                os.mkdir(path)

        def list_to_str(list_str):
            iter_list = iter(list_str)
            temp = next(iter_list)
            for temp_str in iter_list:
                temp += f'、{temp_str}'
            return temp

        def url_download(url, headers, name, path):
            if os.path.isfile(f'{path}/{name}'):
                # print(f'{name}已存在')
                return
            else:
                # print(f'正在下载{name}')
                pass
            response = requests.get(
                url=url,
                headers=headers
            )
            with open(f'{path}/{name}', 'wb') as file:
                file.write(response.content)

        first_path = './terra-historicus'
        is_path(first_path)
        for comic_info in self.__get_comic_info():
            second_path = f'{first_path}/{path_detection.sub("!", comic_info["title"])}'
            is_path(second_path)

            url_download(
                url=comic_info['cover'],
                headers=self.headers,
                name=f'封面.{comic_info["cover"].split(".")[-1]}',
                path=second_path
            )

            # 网站更新时间已改成发布时间，故无法使用更新时间判断
            # if os.path.isfile(second_path + '/info.txt'):
            #     with open(second_path + '/info.txt', 'r', encoding='utf-8') as f:
            #         old_time = time.mktime(time.strptime(f.read().split('\n')[-1].split('：')[-1], '%Y-%m-%d %X'))
            #         upgrade_time = comic_info['updateTime']
            #         if old_time >= upgrade_time:
            #             # print(f'{comic_info["title"]}已经是最新')
            #             continue

            if not os.path.isfile(second_path + '/' + 'info.txt'):
                with open(second_path + '/info.txt', 'w', encoding='utf-8') as f:
                    f.write(f'作品标题：{comic_info["title"]}\n')
                    f.write(f'作品副标题：{comic_info["subtitle"]}\n')
                    f.write(f'作者：{list_to_str(comic_info["authors"])}\n')
                    # f-string表达式不能出现反斜杠，用format方法替换
                    f.write('作品介绍：{}\n'.format(
                        comic_info['introduction'].replace("\n", "\n                ")))
                    f.write(f'作品标签：{list_to_str(comic_info["keywords"])}\n')
                    f.write(f'阅读方向：{comic_info["direction"]}\n')
                    f.write(f'发布时间：{time.strftime("%Y-%m-%d %X", time.localtime(comic_info["updateTime"]))}')
                    
            i = 1
            for episode in tqdm(comic_info['episodes'][::-1], desc=f'{comic_info["title"]}'):
                third_path = f'{second_path}/{i}-{path_detection.sub("!", str(episode["shortTitle"]))} ' \
                             f'{path_detection.sub("!", str(episode["title"]))}'
                is_path(third_path)
                page_nums = self.__get_comic_pages(
                    comic_info['cid'], episode['cid'])
                i += 1
                p = 1
                for url in self.__get_comic_data(comic_info['cid'], episode['cid'], page_nums):
                    if not os.path.isfile(third_path + f'/{p + 1}.{url.split(".")[-1]}'):
                        url_download(
                            url=url,
                            headers=self.headers,
                            name=f'P{p}.{url.split(".")[-1]}',
                            path=third_path
                        )
                    p += 1
        pass


if __name__ == '__main__':
    TH = TerraHistoricus()
    TH.save_data()
    pass
