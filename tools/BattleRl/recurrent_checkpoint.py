"""Complete NumPy training checkpoint; caller owns formal environment replay.

Save at a declared update boundary. A rule/catalog/feature/task-goal signature
must be included in metadata; loading a different signature fails closed.
"""
import json
from pathlib import Path
import numpy as np


class Adam:
    def __init__(self, parameters, learning_rate=5e-5, beta1=.9, beta2=.999,
                 epsilon=1e-8, max_norm=.5):
        self.parameters = parameters
        self.learning_rate, self.beta1, self.beta2 = learning_rate, beta1, beta2
        self.epsilon, self.max_norm = epsilon, max_norm
        self.steps = 0
        self.first = {key: np.zeros_like(value) for key, value in parameters.items()}
        self.second = {key: np.zeros_like(value) for key, value in parameters.items()}

    def update(self, gradients):
        if gradients.keys() != self.parameters.keys():
            raise ValueError("All parameter gradients are required")
        if any(g.shape != self.parameters[k].shape or not np.isfinite(g).all()
               for k, g in gradients.items()):
            raise ValueError("Nonfinite or incompatible gradients")
        norm = np.sqrt(sum(float(np.sum(g * g)) for g in gradients.values()))
        scale = min(1., self.max_norm / (norm + 1e-12))
        self.steps += 1
        for key, parameter in self.parameters.items():
            gradient = gradients[key] * scale
            self.first[key] *= self.beta1
            self.first[key] += (1 - self.beta1) * gradient
            self.second[key] *= self.beta2
            self.second[key] += (1 - self.beta2) * gradient ** 2
            first = self.first[key] / (1 - self.beta1 ** self.steps)
            second = self.second[key] / (1 - self.beta2 ** self.steps)
            parameter -= self.learning_rate * first / (np.sqrt(second) + self.epsilon)

    def settings(self):
        return {key: getattr(self, key) for key in (
            "learning_rate", "beta1", "beta2", "epsilon", "max_norm")}


def save_checkpoint(path, optimizer, random, metadata, replay_state):
    if not all(key in metadata for key in ("rules_sha256", "catalog_sha256",
                                          "feature_schema", "architecture", "task_goal")):
        raise ValueError("Checkpoint must identify original rules, inputs and task goal")
    header = {"version": 1, "metadata": metadata, "optimizer": optimizer.settings(),
              "optimizer_steps": optimizer.steps, "random_state": random.bit_generator.state,
              # Parameter order participates in deterministic reductions and RNG
              # consumers. Preserve it, rather than alphabetically rebuilding it.
              "replay_state": replay_state, "parameter_keys": list(optimizer.parameters)}
    arrays = {"header": np.array(json.dumps(header, sort_keys=True))}
    for prefix, values in (("parameter", optimizer.parameters), ("first", optimizer.first),
                           ("second", optimizer.second)):
        arrays.update({f"{prefix}/{key}": value for key, value in values.items()})
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".pending")
    with temporary.open("wb") as stream:
        np.savez_compressed(stream, **arrays)
    temporary.replace(path)


def load_checkpoint(path, expected_metadata):
    with np.load(path, allow_pickle=False) as arrays:
        header = json.loads(str(arrays["header"]))
        if header["version"] != 1 or header["metadata"] != expected_metadata:
            raise ValueError("Checkpoint rules, inputs, architecture or goal differ")
        keys = header["parameter_keys"]
        expected = {"header"} | {f"{prefix}/{key}" for prefix in
                                  ("parameter", "first", "second") for key in keys}
        if set(arrays.files) != expected:
            raise ValueError("Incomplete optimizer checkpoint")
        parameters = {key: arrays[f"parameter/{key}"].copy() for key in keys}
        optimizer = Adam(parameters, **header["optimizer"])
        for prefix, target in (("first", optimizer.first), ("second", optimizer.second)):
            for key in keys:
                value = arrays[f"{prefix}/{key}"]
                if value.shape != parameters[key].shape or value.dtype != parameters[key].dtype:
                    raise ValueError("Optimizer shape or dtype differs from parameter")
                target[key] = value.copy()
        optimizer.steps = header["optimizer_steps"]
    random = np.random.default_rng()
    random.bit_generator.state = header["random_state"]
    return parameters, optimizer, random, header["replay_state"]
