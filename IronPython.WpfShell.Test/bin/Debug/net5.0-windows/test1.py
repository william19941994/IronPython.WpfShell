#import numpy as np  # injected by c# code 
import os  # import file in lib.zip (in search path)
os.path.abspath(__file__)

a1=[1,2,3]
a2=np.full((2,5),88)    # np is injected by c# code not need import. 
a3=np.sum(a2) 
print(a3)  # print 

dir() # not print in file, only print in command line input.
