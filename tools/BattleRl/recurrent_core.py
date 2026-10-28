"""Three-layer encoder and GRU with sequence gradients, using local NumPy.

No game simulation or hidden audit state is used here. The PPO trainer must
provide public observations/history inputs, initial memory and episode masks.
This is the recurrent core, not a completed PPO actor, optimizer or trainer.
"""
import numpy as np


def sigmoid(x):
    small = np.exp(-np.abs(x))
    return np.where(x >= 0, 1 / (1 + small), small / (1 + small))


class EncoderGRU:
    def __init__(self, input_size, encoder_sizes=(256, 256, 128), hidden_size=256,
                 seed=7, dtype=np.float64):
        if len(encoder_sizes) != 3 or min(input_size, hidden_size, *encoder_sizes) <= 0:
            raise ValueError("Exactly three positive encoder widths are required")
        self.input_size, self.hidden_size = input_size, hidden_size
        self.encoder_sizes = tuple(encoder_sizes)
        random = np.random.default_rng(seed)
        self.parameters = {}
        width = input_size
        for layer, output in enumerate(encoder_sizes):
            self.parameters[f"encoder_w{layer}"] = random.normal(
                0, np.sqrt(2 / (width + output)), (width, output)).astype(dtype)
            self.parameters[f"encoder_b{layer}"] = np.zeros(output, dtype=dtype)
            width = output
        for gate in ("z", "r", "n"):
            self.parameters[f"w{gate}"] = random.normal(
                0, np.sqrt(2 / (width + hidden_size)), (width, hidden_size)).astype(dtype)
            self.parameters[f"u{gate}"] = random.normal(
                0, np.sqrt(1 / hidden_size), (hidden_size, hidden_size)).astype(dtype)
            self.parameters[f"b{gate}"] = np.zeros(hidden_size, dtype=dtype)

    def forward(self, inputs, initial_memory=None, episode_starts=None):
        inputs = np.asarray(inputs)
        if inputs.ndim != 3 or inputs.shape[2] != self.input_size:
            raise ValueError("Inputs must be [time, batch, input_size]")
        time, batch, _ = inputs.shape
        if not time or not batch:
            raise ValueError("Empty sequences cannot train PPO")
        p = self.parameters
        dtype = p["wz"].dtype
        memory = (np.zeros((batch, self.hidden_size), dtype=dtype)
                  if initial_memory is None else np.asarray(initial_memory, dtype=dtype).copy())
        if memory.shape != (batch, self.hidden_size):
            raise ValueError("Initial memory dimensions differ")
        starts = (np.zeros((time, batch), dtype=bool) if episode_starts is None
                  else np.asarray(episode_starts, dtype=bool))
        if starts.shape != (time, batch):
            raise ValueError("Episode starts must be [time, batch]")
        outputs, cache = [], []
        for index in range(time):
            previous = memory * (~starts[index])[:, None]
            activations = [inputs[index]]
            for layer in range(3):
                activations.append(np.tanh(activations[-1] @ p[f"encoder_w{layer}"]
                                           + p[f"encoder_b{layer}"]))
            encoded = activations[-1]
            z = sigmoid(encoded @ p["wz"] + previous @ p["uz"] + p["bz"])
            r = sigmoid(encoded @ p["wr"] + previous @ p["ur"] + p["br"])
            # Reset-before-linear GRU variant; keep this equation with its gradients.
            n = np.tanh(encoded @ p["wn"] + (r * previous) @ p["un"] + p["bn"])
            memory = z * previous + (1 - z) * n
            outputs.append(memory.copy())
            cache.append((activations, previous, z, r, n, ~starts[index]))
        return np.stack(outputs), memory.copy(), cache

    def backward(self, output_gradients, cache):
        gradients = np.asarray(output_gradients)
        if len(cache) != len(gradients) or gradients.ndim != 3:
            raise ValueError("Gradient sequence must match the forward sequence")
        p = self.parameters
        parameter_gradients = {key: np.zeros_like(value) for key, value in p.items()}
        carry = np.zeros_like(gradients[0])
        input_gradients = []
        for index in range(len(cache) - 1, -1, -1):
            activations, previous, z, r, n, keep = cache[index]
            encoded = activations[-1]
            dh = gradients[index] + carry
            gn = dh * (1 - z) * (1 - n * n)
            gz = dh * (previous - n) * z * (1 - z)
            reset_previous_grad = gn @ p["un"].T
            gr = reset_previous_grad * previous * r * (1 - r)
            for gate, gate_gradient, recurrent_input in (
                    ("n", gn, r * previous), ("r", gr, previous), ("z", gz, previous)):
                parameter_gradients[f"w{gate}"] += encoded.T @ gate_gradient
                parameter_gradients[f"u{gate}"] += recurrent_input.T @ gate_gradient
                parameter_gradients[f"b{gate}"] += gate_gradient.sum(axis=0)
            carry = (dh * z + reset_previous_grad * r
                     + gr @ p["ur"].T + gz @ p["uz"].T) * keep[:, None]
            de = gn @ p["wn"].T + gr @ p["wr"].T + gz @ p["wz"].T
            for layer in range(2, -1, -1):
                de *= 1 - activations[layer + 1] ** 2
                parameter_gradients[f"encoder_w{layer}"] += activations[layer].T @ de
                parameter_gradients[f"encoder_b{layer}"] += de.sum(axis=0)
                de = de @ p[f"encoder_w{layer}"].T
            input_gradients.append(de)
        return np.stack(input_gradients[::-1]), carry, parameter_gradients
