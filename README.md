# UnityMultiplayerARPG_2DAI

AI implement for 2D mode, which implements [A* Pathfinding Project](https://arongranberg.com/astar/)

Their package files will not included in this repository, visit their site to download it :)

## Setup Player Character Entity / Monster Character Entity

- Remove other entity movement components
- Attach `Astar Character Movement 2D` to your character entity

## Setup map scene

- Create empty game and attach `Pathfinder` component

![](./DocsMaterials/3.png)

- In `Graph` section select `Grid Graph`

![](./DocsMaterials/4.png)

- In `Grid Graph` setting, enable `2D` and `Use 2D Physics`

![](./DocsMaterials/5.png)

- Then set `Width`, `Depth`, `Node size` and `Center`, make it cover the map

![](./DocsMaterials/6.png)

![](./DocsMaterials/7.png)

- Set `Collider type` to `Point`
- Set `Obstacle Layer Mask` by select layers that you want to make it as obstacles
