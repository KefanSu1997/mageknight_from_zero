"""Stop before drawing new tasks, saving the last complete optimizer boundary."""
def protect_boundary(*, free_bytes, completed_updates, save, publish, minimum_bytes=3*1024**3):
    if free_bytes >= minimum_bytes:
        return False
    # A failed save must propagate; never report a resumable boundary without it.
    receipt = save(completed_updates)
    publish(dict(reason='disk_protection', completed_updates=completed_updates,
                 free_bytes=free_bytes, minimum_bytes=minimum_bytes,
                 checkpoint=receipt, resumable=True))
    return True
