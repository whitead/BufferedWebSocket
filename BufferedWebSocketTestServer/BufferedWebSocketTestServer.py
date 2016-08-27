from tornado import websocket, web, ioloop, gen
import numpy as np
import struct, logging, uuid
from concurrent.futures import ThreadPoolExecutor


thread_pool = ThreadPoolExecutor(4)

available_data = set()
loaded_data = dict()
loaded_data_iters = dict()
inuse_data = set()


def prepare_datasets():
    available_data.add('test')



FRAME_BYTES = 4
DATA_LENGTH_BYTES = 4
CHUNK_BYTES = 4
WS_TTL = 1000
WS_PROTOCOL = 'ws://'
_DEBUG_LEN = 10

def load_dataset(name, key):
    assert name == 'test', 'no dataset loader has been written'
    return np.random.rand(_DEBUG_LEN).astype(np.float32)

def get_max_buffer_size(key):
    return len(loaded_data[key])
    
def increment_iterator(key):
    if(loaded_data_iters[key] == 10):
        raise StopIteration()
    loaded_data_iters[key] += 1
    loaded_data[key] = np.random.rand(_DEBUG_LEN).astype(np.float32)

def reset_iterator(key):
    loaded_data_iters[key] = 0

def get_frame(key, i):
    u = loaded_data[key]
    j = loaded_data_iters[key]
    if(i != j):
        if(i == j + 1):
            logging.log(logging.INFO, 'Frame is next, iterating')
            increment_iterator(key)
        else:
            logging.log(logging.INFO, 'Frame is random access. Resetting iterator and fast-forward')                    
            reset_iterator(key)
            for _ in range(i):
                increment_iterator(key)
    else:
        logging.log(logging.INFO, 'Frame is same')
    return u

def pack_message(frame, data):
    return struct.pack('!ii', frame, len(data)) + data
    

class IndexHandler(web.RequestHandler):
    def get(self):
        self.render('templates/index.html')
        
class SInfoHandler(web.RequestHandler):
    
    @gen.coroutine
    def get(self, name):
        print(name, available_data)
        if name in available_data:
            key = str(uuid.uuid4())
            self.set_cookie('key', key)
            loaded_data[key] = yield thread_pool.submit(load_dataset, name, key)
            reset_iterator(key)
            self._write_info(name, key)
            self.flush()
        else:
            self.send_error(404, simname=name)
                    
    def _write_info(self, name, key):
        data = {'name': name, 'id': key,
                'ws_url': WS_PROTOCOL + self.request.host + '/sim/' + key + '/pos',
                'key_bytes': FRAME_BYTES,
                'len_bytes': DATA_LENGTH_BYTES,
                'max_buffer_size': get_max_buffer_size(key) * CHUNK_BYTES,
                'elements': ['O' for _ in range(_DEBUG_LEN)],
                'atom_number': _DEBUG_LEN,
                'frame_number': 100
                }
        self.write(data)
    
    def write_error(self, status_code, **kwargs):
        if status_code == 404:
            self.write('Could not find dataset ' + kwargs['simname'])
            self.flush()
    

class SocketHandler(websocket.WebSocketHandler):    

    def check_origin(self, origin):
        return True

    def open(self, key = None):
        if key is None:
            key = self.get_cookie('key')
        self.key =  key
        self.timeout = None
        
        if key not in loaded_data:
            logging.log(logging.INFO, 'Could not find dataset in loaded data, closing socket')
            self.close(404, 'no sim')
        else:
            logging.log(logging.INFO, 'dataset found, setting pings and setting sim to be in use')            
            inuse_data.add(key)
            #keep alice connection
            ioloop.IOLoop.current().call_later(WS_TTL * 1000 / 2, self.ping, 'ping')
            logging.log(logging.INFO, 'Awaiting request')            
            
    def ping(self, data):
        super().ping(data)
        if self.timeout is not None:
            ioloop.IOLoop.current().remove_timeout(self.timeout)        
        self.timeout = ioloop.IOLoop.current().add_timeout(
            datetime.timedelta(milliseconds=WS_TTL * 1000 / 2), self.close)
        
    def pong(self):        
        if self.timeout is not None:
            ioloop.IOLoop.current().remove_timeout(self.timeout)
        ioloop.IOLoop.current().call_later(WS_TTL * 1000 / 2, self.ping, 'ping')            

    def on_close(self):
        if self.key in inuse_data:
            del loaded_data[self.key]
            inuse_data.remove(self.key)            
        if self.timeout is not None:
            ioloop.IOLoop.current().remove_timeout(self.timeout)
        
                 
            
    @gen.coroutine
    def on_message(self, message):
        valid = True
        u = None
        try:
            logging.log(logging.INFO, 'Received message, parsing ' + str(message))
            req_frame, =  struct.unpack('!i', message)
            logging.log(logging.INFO, 'Requested frame is ' + str(req_frame))
            #try to load the frame
            u = get_frame(self.key, req_frame)
        except struct.error as e:
            valid = False
            logging.log(logging.WARN, 'Could not decode request frame: ' + message)
            logging.log(logging.WARN, str(e))
        except StopIteration:
            logging.log(logging.WARN, 'Info frame is outside of available frames')
            #send an empty frame to indicate this
            m = pack_message(req_frame, b'')
            self.write_message(m, binary=True)
            logging.log(logging.INFO, 'sent message of 0 positions')
            valid = False
        except Exception as e:
            valid = False
            logging.log(logging.WARN, str(e))
            
        if valid:
            data = u.tobytes()
            logging.log(logging.INFO, 'Found frame and am sending...')
            m = pack_message(req_frame, data)
            self.write_message(m, binary=True)
            logging.log(logging.INFO, 'sent')
        
        
app = web.Application([
    (r'/', IndexHandler),
    (r'/sim/pos', SocketHandler),    
    (r'/sim/(.*)/pos', SocketHandler),    
    (r'/sim/(.*)', SInfoHandler),
    (r'/static/js/(.*)', web.StaticFileHandler, {'path': 'js'})],
                      debug = True)

if __name__ == '__main__':
    logging.basicConfig(format='%(levelname)s [%(name)s] %(message)s', level=logging.DEBUG)
    logging.log(logging.INFO, 'Loading datasets')
    prepare_datasets()
    logging.log(logging.INFO, 'Ready, starting server')    
    app.listen(8888)
    ioloop.IOLoop.instance().start()

