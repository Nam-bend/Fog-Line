from pathlib import Path
import re, uuid, copy, yaml

ROOT=Path(__file__).resolve().parents[2]
folder=ROOT/'Assets/_Project/Animations/Enemies/MotherMoster'
folder.mkdir(parents=True,exist_ok=True)
model=ROOT/'Assets/_Project/Art/Enemies/MotherMosterPSX/MotherMosterPSX.fbx.meta'
meta=yaml.safe_load(model.read_text(encoding='utf-8-sig'))
importer=meta['ModelImporter']
names=['Idle','Walk','Run','Attack','Stagger','Death','Attack_Sweep','Attack_Combo','Attack_Slam']
lengths=[96,36,20,36,24,72,33,48,45]
attacks=['Attack','Attack_Sweep','Attack_Combo','Attack_Slam']
strikes={'Attack':[.5],'Attack_Sweep':[16/30],'Attack_Combo':[.5,28/30],'Attack_Slam':[.75]}
ids={name:7400000+i*2 for i,name in enumerate(names)}
importer['importAnimation']=1
importer['animations']['animationCompression']=0
importer['internalIDToNameTable']=[{'first':{74:ids[n]},'second':n} for n in names]
clips=[]
for n,length in zip(names,lengths):
    clip={'serializedVersion':16,'name':n,'takeName':'MotherMoster_Rig|MotherMoster_'+n,'internalID':ids[n],
          'firstFrame':0,'lastFrame':length,'wrapMode':0,'orientationOffsetY':0,'level':0,'cycleOffset':0,
          'loop':0,'hasAdditiveReferencePose':0,'loopTime':int(n in names[:3]),'loopBlend':int(n in names[:3]),
          'loopBlendOrientation':1,'loopBlendPositionY':1,'loopBlendPositionXZ':1,
          'keepOriginalOrientation':1,'keepOriginalPositionY':1,'keepOriginalPositionXZ':1,'heightFromFeet':0,
          'mirror':0,'bodyMask':'01000000010000000100000001000000010000000100000001000000010000000100000001000000010000000100000001000000',
          'curves':[],'events':[],'transformMask':[],'maskType':3,'maskSource':{'instanceID':0},'additiveReferencePoseFrame':0}
    if n in strikes:
        clip['events']=[{'time':time/(length/30),'functionName':'OnMonsterStrike','data':'','objectReferenceParameter':{'fileID':0},'floatParameter':0,'intParameter':0,'messageOptions':0} for time in strikes[n]]
    clips.append(clip)
importer['animations']['clipAnimations']=clips
model.write_text(yaml.safe_dump(meta,sort_keys=False,width=200),encoding='utf-8')

# Use the project's own controller schema; bind clips to explicit FBX internal IDs.
source=(ROOT/'Assets/_Project/Animations/Enemies/MonsterPSX.controller').read_text(encoding='utf-8-sig')
documents=[]
for match in re.finditer(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)',source,re.S):
    documents.append((int(match[1]),int(match[2]),yaml.safe_load(match[3])))
def template(kind): return copy.deepcopy(next(d for k,i,d in documents if k==kind))
stateids={n:1102000+i for i,n in enumerate(names)}
machineid=1107000
out=[]
states={}
for n in names:
    doc=template(1102); st=doc['AnimatorState']; st['m_Name']=n; st['m_Transitions']=[]
    st['m_WriteDefaultValues']=0
    st['m_Motion']={'fileID':ids[n],'guid':meta['guid'],'type':3}
    if n in attacks:
        st['m_Tag']='Attack'
        behaviour_id=1140000+attacks.index(n)
        st['m_StateMachineBehaviours']=[{'fileID':behaviour_id}]
        out.append((114,behaviour_id,{'MonoBehaviour':{'m_ObjectHideFlags':1,'m_CorrespondingSourceObject':{'fileID':0},'m_PrefabInstance':{'fileID':0},'m_PrefabAsset':{'fileID':0},'m_GameObject':{'fileID':0},'m_Enabled':1,'m_EditorHideFlags':0,'m_Script':{'fileID':11500000,'guid':'a76423039d8e4e58bb8b6b770d2c3ae1','type':3},'m_Name':'','m_EditorClassIdentifier':'Assembly-CSharp::MotherAttackVariation'}}))
    states[n]=st; out.append((1102,stateids[n],doc))
transitions=[]
def transition(a,b,conditions=(),exit=False,duration=.12):
    tid=1101000+len(transitions)
    doc=template(1101); tr=doc['AnimatorStateTransition']
    tr['m_Conditions']=[{'m_ConditionMode':condition[1],'m_ConditionEvent':condition[0],'m_EventTreshold':condition[2] if len(condition)>2 else 0} for condition in conditions]
    tr['m_DstState']={'fileID':stateids[b]}; tr['m_HasExitTime']=int(exit); tr['m_ExitTime']=1
    tr['m_TransitionDuration']=duration; tr['m_CanTransitionToSelf']=0
    states[a]['m_Transitions'].append({'fileID':tid}); transitions.append((1101,tid,doc))
for n in [n for n in names if n!='Death']:
    transition(n,'Death',[('Die',1)],duration=.1)
    if n!='Stagger': transition(n,'Stagger',[('Stagger',1)],duration=.05)
    if n not in attacks+['Stagger']:
        for i,attack in enumerate(attacks): transition(n,attack,[('Attack',1),('AttackVariant',6,i)],duration=.06)
transition('Idle','Walk',[('IsMoving',1),('IsWalking',1)])
transition('Idle','Run',[('IsMoving',1),('IsWalking',2)])
for n in ['Walk','Run']: transition(n,'Idle',[('IsMoving',2)])
transition('Walk','Run',[('IsWalking',2)])
transition('Run','Walk',[('IsWalking',1)])
for n in attacks+['Stagger']: transition(n,'Idle',exit=True,duration=.08)
out+=transitions
doc=template(1107); sm=doc['AnimatorStateMachine']
sm['m_ChildStates']=[{'serializedVersion':1,'m_State':{'fileID':stateids[n]},'m_Position':{'x':250+(i%3)*230,'y':80+(i//3)*160,'z':0}} for i,n in enumerate(names)]
sm['m_AnyStateTransitions']=[]; sm['m_DefaultState']={'fileID':stateids['Idle']}
out.append((1107,machineid,doc))
doc=template(91); ctrl=doc['AnimatorController']; ctrl['m_Name']='MotherMoster'
ctrl['m_AnimatorParameters']=[{'m_Name':n,'m_Type':typ,'m_DefaultFloat':0,'m_DefaultInt':0,'m_DefaultBool':0,'m_Controller':{'fileID':9100000}} for n,typ in [('IsMoving',4),('IsWalking',4),('AttackVariant',3),('Attack',9),('Stagger',9),('Die',9)]]
ctrl['m_AnimatorLayers'][0]['m_StateMachine']={'fileID':machineid}
out.append((91,9100000,doc))
path=folder/'MotherMoster.controller'
path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join(f'--- !u!{k} &{i}\n'+yaml.safe_dump(d,sort_keys=False,width=200) for k,i,d in out),encoding='utf-8')
metapath=Path(str(path)+'.meta')
if not metapath.exists(): metapath.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 9100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
assert not states['Death']['m_Transitions']
assert len(importer['animations']['clipAnimations'])==len(names)
assert {st['m_Motion']['fileID'] for st in states.values()}==set(ids.values())
print('Unity metadata/controller configured. Editor validation pending license activation.')
