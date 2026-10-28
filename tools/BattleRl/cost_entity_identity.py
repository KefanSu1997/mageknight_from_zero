"""Alpha-rename audit entity labels, preserving actual entity relationships.

RL legality previews allocate IDs for speculative objects; UI setup does not
perform those previews. Label gaps are not gameplay differences. Require a
bijection from ordered hand/draw and all native zones; never erase references.
"""
import copy


def align(expected,actual):
    cards={};enemies={}
    def bind(mapping,a,b,description):
        if a in mapping and mapping[a]!=b:raise ValueError('Conflicting '+description+' identity')
        if b in mapping.values() and mapping.get(a)!=b:raise ValueError('Nonbijective '+description+' identity')
        mapping[a]=b
    for zone,es in expected.get('zones',{}).items():
        ac=actual['zones'].get(zone,[])
        if len(es)!=len(ac):raise ValueError('Different native zone size: '+zone)
        for e,a in zip(es,ac):
            if {k:v for k,v in e.items() if k!='instance'}!={k:v for k,v in a.items() if k!='instance'}:
                raise ValueError('Different ordered physical zone contents: '+zone)
            bind(cards,a['instance'],e['instance'],'card')
    def enemy_list(es,ac):
        if len(es)!=len(ac):raise ValueError('Different physical enemy count')
        for e,a in zip(es,ac):
            if e.get('id')!=a.get('id'):raise ValueError('Different physical enemy order')
            bind(enemies,a['instance'],e['instance'],'enemy')
    enemy_list(expected.get('enemies',[]),actual.get('enemies',[]))
    for zone in ('draw','discard','inUse'):
        enemy_list(expected.get('enemySupply',{}).get(zone,[]),actual.get('enemySupply',{}).get(zone,[]))
    for zone in ('puppetCandidates','puppetStored'):
        enemy_list(expected.get(zone,[]),actual.get(zone,[]))
    result=copy.deepcopy(actual)
    def relabel(mapping,v):
        if v==-1 or v is None:return v
        if v not in mapping:raise ValueError('Audit reference has no matched physical entity: '+str(v))
        return mapping[v]
    for zone,items in result.get('zones',{}).items():
        for x in items:x['instance']=relabel(cards,x['instance'])
    for key in ('handOrder','auditDrawOrder'):result[key]=[relabel(cards,v) for v in result.get(key,[])]
    for key in ('pendingHeadband','mysticBox'):
        if result.get(key):result[key]['physical']=relabel(cards,result[key]['physical'])
    if result.get('paymentSelection'):result['paymentSelection']['material']=relabel(cards,result['paymentSelection']['material'])
    for key in ('attacks','blocks'):
        for x in result.get(key,[]):
            if 'cardInstance' in x:x['cardInstance']=relabel(cards,x['cardInstance'])
            if 'requiredEnemy' in x:x['requiredEnemy']=relabel(enemies,x['requiredEnemy'])
    for key in ('enemies','puppetCandidates','puppetStored'):
        for x in result.get(key,[]):x['instance']=relabel(enemies,x['instance'])
    for items in result.get('enemySupply',{}).values():
        if isinstance(items,list):
            for x in items:x['instance']=relabel(enemies,x['instance'])
    if result.get('hardened'):result['hardened']['enemy']=relabel(enemies,result['hardened']['enemy'])
    for key in ('defenderApplied','defenderSelected'):
        for x in result.get(key,[]):
            x['source']=relabel(enemies,x['source']);x['target']=relabel(enemies,x['target'])
    shared=result.get('sharedSkills',{})
    for key in ('painWounds',):
        if key in shared:shared[key]=[relabel(cards,v) for v in shared[key]]
    return result,{'card_labels':cards,'enemy_labels':enemies}
