#import numpy as np  # injected by c# code 
import os
os.path.abspath(__file__)

a1=[1,2,3]
a2=np.full((2,5),88)
a3=np.sum(a2)
print(a3) 

dir()
