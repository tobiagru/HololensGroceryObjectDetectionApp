# Hololens Object Detection App
This is a UWP app that runs on Hololens and performs object detection on a remote Tensorflow Extended (TFX) serving server which runs a object detection model. Can be used with REST or gRPC. gRPC + GPU server leads to near realtime performance.

This app served as a demo case for this publication: https://www.researchgate.net/profile/Klaus_Fuchs2/publication/337068624_Towards_Identification_of_Packaged_Products_via_Computer_Vision_Convolutional_Neural_Networks_for_Object_Detection_and_Image_Classification_in_Retail_Environments/links/5dc7de0692851c81803f4126/Towards-Identification-of-Packaged-Products-via-Computer-Vision-Convolutional-Neural-Networks-for-Object-Detection-and-Image-Classification-in-Retail-Environments.pdf 

To build the application you need Unity 2017.4 and the HoloToolKit. And a Google Object Detection API Model (https://github.com/tensorflow/models/tree/master/research/object_detection) that runs on a Tensorflow Serving Server (https://www.tensorflow.org/tfx/guide/serving). You the paper lists more on the evaluation, the repository to train models can be found here: https://github.com/tobiagru/HoloselectaObjectDetection  

The Architecture is a queue system where each class acts as an async singleton with a queue attached that stores enough assets to serve the next prediction round. The flow is like this:  
Image Capture ---> Image Serializer --> Image Analyser --> TFX Server --> Image Analyser --> Object Storage

The bottleneck beside the transfer to the server and the analysis is the image capture which is relatively slow. But this can definitly be improved.
