```
-------------------Commands Sample---------------------------------  
----  u <f> | d <f> | c <carId> <dest>   (Ctrl+C to quit)  -------  
 Ex 1: passenger is in 1st floor and wants to go up in 9th floor  
u 1     -> hall-call for "Up", say car 2 (nearest) will go down to 1st floor for pickup, if car 2 is in 6th floor, 5 x 10s = 50s waiting time  
c 2 9   -> next, instruct car 2 to go to 9th floor  

Ex 2: passenger is in 10th floor and wants to go down in 5th floor  
d 10     -> hall-call for "Down", say car 4 (nearest) will go up to 10th floor for pickup  
c 4 5    -> next, instruct car 4 to go to 5th floor

Ex 3: simulate two passengers pickup   
u 1     -> hall-call for "Up" p1  
u 3     -> hall-call for "Up" p2  
c 2 7   -> board p2 first in f 3
        -> car moved down to pick up p1  
c 2 5   -> board p1 then car move up  
        -> car stopped at 5 for p1   
        -> car stopped at 7 for p2  
-------------------------------------------------------------------  

```
