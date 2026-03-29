
extends Node
# 这个脚本你需要挂到游戏的Autoload才能全局读表


static func loader(path:String):
    var file = FileAccess.open(path,FileAccess.READ)
    var txt = file.get_as_text()
    var data = JSON.parse_string(txt)
    file.close()
    return data

var enemy = loader('res://tables/dist/enemy/enemy.json')
var hero = loader('res://tables/dist/hero/hero.json')
var shop = loader('res://tables/dist/shop/shop.json')
var synergy = loader('res://tables/dist/synergy/synergy.json')
var wave = loader('res://tables/dist/wave/wave.json')
