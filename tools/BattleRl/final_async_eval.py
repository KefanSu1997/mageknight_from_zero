"""Publish an immutable checkpoint for the independent 32-worker evaluator."""
import json,hashlib,shutil,os
def frozen_evaluation(model, rows, history):
    jobs=model.output/'eval_jobs';jobs.mkdir(exist_ok=True)
    path=jobs/f'checkpoint_{model.completed_updates:04d}.zip'
    temp=path.with_suffix('.pending')
    shutil.copyfile(model.output/'checkpoint.zip',temp);os.replace(temp,path)
    job=dict(update=model.completed_updates,checkpoint=str(path.resolve()),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),episodes=1000,workers=32)
    pending=jobs/f'{model.completed_updates:04d}.json.pending'
    pending.write_text(json.dumps(job));os.replace(pending,jobs/f'{model.completed_updates:04d}.json')
