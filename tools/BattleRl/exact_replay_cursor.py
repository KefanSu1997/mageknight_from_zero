"""Reuse only the live ancestor prefix in the same real rule process."""


class ExactReplayCursor:
    def __init__(self,rules,task):
        self.rules,self.task=rules,task
        self.prefix,self.current=(),None
        self.step_calls,self.reset_calls,self.reused_operations=0,0,0
        self.last_before,self.attempted_prefix=None,()

    def replay(self,prefix,check_budget=lambda:None):
        prefix=tuple(prefix)
        check_budget()
        self.attempted_prefix=prefix
        if self.current is None or prefix[:len(self.prefix)]!=self.prefix:
            self.current=self.rules.reset_task(self.task)
            self.prefix=()
            self.reset_calls+=1
        else:
            self.reused_operations+=len(self.prefix)
        for action in prefix[len(self.prefix):]:
            check_budget()
            self.last_before=self.current['state']
            if action not in self.current['legal']:
                raise RuntimeError('Exact prefix requested an illegal action')
            self.current=self.rules.step(action)
            self.prefix+=(action,)
            self.step_calls+=1
        return self.current
