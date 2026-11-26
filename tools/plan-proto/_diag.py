import collections
import plan_proto as pp
for cfg in pp.floor_programs():
    fl = pp.make_floor(cfg)
    kinds = collections.Counter(k for k, _, _ in fl['corridors'])
    cm = fl['corridor_mask']
    print(cfg['name'], dict(kinds), 'corr_cells=', int(cm.sum()))
    for room in fl['rooms']:
        if room.get('landmark'):
            print('   landmark', room['kind'], room.get('label'),
                  'area=%.0f' % room['area'], 'doors=', len(room.get('doors') or []),
                  [round(d[1], 1) for d in (room.get('doors') or [])])
