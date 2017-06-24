 precision mediump float;

 uniform vec2 resolution;

 uniform vec3 lightColor;
 uniform vec3 specularColor;
 uniform vec3 diffuseColor;

 uniform float roughness;
 uniform float intensity;
 uniform float width;
 uniform float height;
 uniform float rotationY;
 uniform float rotationZ;
 uniform bool twoSided;

 uniform mat4 view;

 uniform sampler2D ltc_mat;
 uniform sampler2D ltc_mag;

 const float PI = 3.1415926535;
 const float GAMMA = 2.2;

 vec3 gamma(vec3 v) { 
  return pow(v, vec3(GAMMA)); 
}

vec3 degamma(vec3 v) { 
  return pow(v, vec3(1.0 / GAMMA)); 
}

vec2 adjustIndex(vec2 index) {
  float LUT_SIZE  = 64.0;
  float LUT_SCALE = (LUT_SIZE - 1.0)/LUT_SIZE;
  float LUT_BIAS  = 0.5/LUT_SIZE;
  return index * LUT_SCALE + LUT_BIAS;
}

mat3 calParameterMatrix(vec3 N, vec3 V, vec2 index) {
  vec4 matTexture = texture2D(ltc_mat, index);
  mat3 paramMatrix = mat3(0.0);
  paramMatrix[0][0] = 1.0;
  paramMatrix[2][2] = matTexture.x;
  paramMatrix[0][2] = matTexture.y;
  paramMatrix[1][1] = matTexture.z;
  paramMatrix[2][0] = matTexture.w;

  return paramMatrix;
}

int ClipQuadToHorizon(inout vec3 L[5]) {
   // detect clipping config
   int config = 0;
   if (L[0].z > 0.0) config += 1;
   if (L[1].z > 0.0) config += 2;
   if (L[2].z > 0.0) config += 4;
   if (L[3].z > 0.0) config += 8;

   // clip
   int n = 0;

   if (config == 0)
   {
    // clip all
}
else if (config == 1) // V1 clip V2 V3 V4
{
  n = 3;
  L[1] = -L[1].z * L[0] + L[0].z * L[1];
  L[2] = -L[3].z * L[0] + L[0].z * L[3];
}
else if (config == 2) // V2 clip V1 V3 V4
{
  n = 3;
  L[0] = -L[0].z * L[1] + L[1].z * L[0];
  L[2] = -L[2].z * L[1] + L[1].z * L[2];
}
else if (config == 3) // V1 V2 clip V3 V4
{
  n = 4;
  L[2] = -L[2].z * L[1] + L[1].z * L[2];
  L[3] = -L[3].z * L[0] + L[0].z * L[3];
}
else if (config == 4) // V3 clip V1 V2 V4
{
  n = 3;
  L[0] = -L[3].z * L[2] + L[2].z * L[3];
  L[1] = -L[1].z * L[2] + L[2].z * L[1];
}
else if (config == 5) // V1 V3 clip V2 V4) impossible
{
  n = 0;
}
else if (config == 6) // V2 V3 clip V1 V4
{
  n = 4;
  L[0] = -L[0].z * L[1] + L[1].z * L[0];
  L[3] = -L[3].z * L[2] + L[2].z * L[3];
}
else if (config == 7) // V1 V2 V3 clip V4
{
  n = 5;
  L[4] = -L[3].z * L[0] + L[0].z * L[3];
  L[3] = -L[3].z * L[2] + L[2].z * L[3];
}
else if (config == 8) // V4 clip V1 V2 V3
{
  n = 3;
  L[0] = -L[0].z * L[3] + L[3].z * L[0];
  L[1] = -L[2].z * L[3] + L[3].z * L[2];
  L[2] =  L[3];
}
else if (config == 9) // V1 V4 clip V2 V3
{
  n = 4;
  L[1] = -L[1].z * L[0] + L[0].z * L[1];
  L[2] = -L[2].z * L[3] + L[3].z * L[2];
}
else if (config == 10) // V2 V4 clip V1 V3) impossible
{
  n = 0;
}
else if (config == 11) // V1 V2 V4 clip V3
{
  n = 5;
  L[4] = L[3];
  L[3] = -L[2].z * L[3] + L[3].z * L[2];
  L[2] = -L[2].z * L[1] + L[1].z * L[2];
}
else if (config == 12) // V3 V4 clip V1 V2
{
  n = 4;
  L[1] = -L[1].z * L[2] + L[2].z * L[1];
  L[0] = -L[0].z * L[3] + L[3].z * L[0];
}
else if (config == 13) // V1 V3 V4 clip V2
{
  n = 5;
  L[4] = L[3];
  L[3] = L[2];
  L[2] = -L[1].z * L[2] + L[2].z * L[1];
  L[1] = -L[1].z * L[0] + L[0].z * L[1];
}
else if (config == 14) // V2 V3 V4 clip V1
{
  n = 5;
  L[4] = -L[0].z * L[3] + L[3].z * L[0];
  L[0] = -L[0].z * L[1] + L[1].z * L[0];
}
else if (config == 15) // V1 V2 V3 V4
{
  n = 4;
}

if (n == 3)
L[3] = L[0];
if (n == 4)
L[4] = L[0];

return n;
}

float IntegrateEdge(vec3 v1, vec3 v2) {
    float cosTheta = dot(v1, v2);
    float theta = acos(cosTheta);    
    float res = cross(v1, v2).z * ((theta > 0.001) ? theta/sin(theta) : 1.0);

    return res;
}

mat3 transpose(mat3 v) {
    mat3 tmp;
    tmp[0] = vec3(v[0].x, v[1].x, v[2].x);
    tmp[1] = vec3(v[0].y, v[1].y, v[2].y);
    tmp[2] = vec3(v[0].z, v[1].z, v[2].z);

    return tmp;
}

vec3 LTC_Evaluate(vec3 N, vec3 V, vec3 P, mat3 param, vec3 points[4]) {
    vec3 baseZ = N;
    vec3 baseX = normalize(V - baseZ * dot(baseZ, V));
    vec3 baseY = cross(baseX, baseZ);

    param = param * transpose(mat3(baseX, baseY, baseZ));

    vec3 polygonVertices[5];
    for(int i = 0; i < 5; i++)
    polygonVertices[i] = param * (points[i] - P);

    int n = ClipQuadToHorizon(polygonVertices);
    if(n == 0) return vec3(0);

    for(int i = 0; i < 5; i++)
    polygonVertices[i] = normalize(polygonVertices[i]);

    float integrateResult = 0.0;
    for(int i = 0; i < 5; i++)
    integrateResult += IntegrateEdge(polygonVertices[i], polygonVertices[i == 4 ? 0 : i + 1]);

    integrateResult = twoSided ? abs(integrateResult) : max(0.0, integrateResult);

    return vec3(integrateResult);
}

vec3 calColor(vec3 pos, vec3 N, vec3 V, vec3 points[4]) {
    vec3 diffuse;
    vec3 specular;

    float theta = acos(dot(N, V)) / (0.5 * PI);
    vec2 index = adjustIndex(vec2(roughness, theta));
    mat3 paramMatrix = calParameterMatrix(N, V, index);

    specular = LTC_Evaluate(N, V, pos, paramMatrix, points);
    specular *= texture2D(ltc_mag, index).w;

    diffuse = LTC_Evaluate(N, V, pos, mat3(1), points);

    return lightColor * (specularColor * specular + diffuseColor * diffuse) / (2.0 * PI);
}

void getRectPoints(out vec3 points[4]) {
    float halfWidth = width / 2.0;
    float halfHeight = height / 2.0;
    vec3 origin = vec3(0);
    vec3 dirX = vec3(1, 0, 0);
    vec3 dirY = vec3(0, 1, 0);

    points[0] = origin - dirX * halfWidth - dirY * halfHeight;
    points[1] = origin - dirX * halfWidth + dirY * halfHeight;
    points[2] = origin + dirX * halfWidth + dirY * halfHeight;
    points[3] = origin + dirX * halfWidth - dirY * halfHeight;
}

struct Ray {
    vec3 origin;
    vec3 direction;
};

struct Plane {
    vec4 form;
};

struct Rect {
    vec3 center;
    vec3 dirX;
    vec3 dirY;
    float width;
    float height;
    Plane plane;
};

bool rayIntersectPlane(Ray ray, Plane plane, out vec3 hitPoint) {
    float t = -dot(plane.form,
      vec4(ray.origin, 1.0) / dot(plane.form.xyz, ray.direction));

    hitPoint = ray.origin + ray.direction * t;

    float esp = 0.0;
    if(t > esp)
    return true;
    else
    return false;
}

bool rayIntersectRect(Ray ray, Rect rect, out vec3 hitPoint) {
    bool isIntersect = rayIntersectPlane(ray, rect.plane, hitPoint);
    if(isIntersect) {
      vec3 relaPos = hitPoint - rect.center;

      float relaX = dot(relaPos, rect.dirX);
      float relaY = dot(relaPos, rect.dirY);

      if(abs(relaX) > rect.width / 2.0 || abs(relaY) > rect.height / 2.0)
      isIntersect = false;
  }

  return isIntersect;
}

Ray getRayFromCamera() {
    Ray ray;

    vec2 pos = 2.0 * (gl_FragCoord.xy / resolution) - vec2(1.0);

    float focal = 2.0;

    ray.origin = vec3(0);
    ray.direction = normalize(vec3(pos, focal) - ray.origin);

    ray.origin = (view*vec4(ray.origin, 1)).xyz;
    ray.direction = (view*vec4(ray.direction, 0)).xyz;

    return ray;
}

vec3 rotation_y(vec3 v, float a)
{
    vec3 r;
    r.x =  v.x*cos(a) + v.z*sin(a);
    r.y =  v.y;
    r.z = -v.x*sin(a) + v.z*cos(a);
    return r;
}

vec3 rotation_z(vec3 v, float a)
{
    vec3 r;
    r.x =  v.x*cos(a) - v.y*sin(a);
    r.y =  v.x*sin(a) + v.y*cos(a);
    r.z =  v.z;
    return r;
}

vec3 rotation_yz(vec3 v, float ay, float az)
{
    return rotation_z(rotation_y(v, ay), az);
}

Rect getRect() {
    Rect rect;

    rect.center = vec3(0, 6, 30);
    rect.dirX = rotation_yz(vec3(1, 0, 0), rotationY * 2.0 * PI, rotationZ * 2.0 * PI);
    rect.dirY = rotation_yz(vec3(0, 1, 0), rotationY * 2.0 * PI, rotationZ * 2.0 * PI);
    rect.width = width;
    rect.height = height;

    vec3 normal = cross(rect.dirX, rect.dirY);
    rect.plane.form = vec4(normal, -dot(normal, rect.center));

    return rect;
}

void getRectPoints(Rect rect, out vec3 points[4]) {

    vec3 vx = rect.dirX * rect.width / 2.0;
    vec3 vy = rect.dirY * rect.height / 2.0;

    points[0] = rect.center - vx - vy;
    points[1] = rect.center + vx - vy;
    points[2] = rect.center + vx + vy;
    points[3] = rect.center - vx + vy;
}

void main() {

    vec3 lightColor = gamma(lightColor);
    vec3 specularColor = gamma(specularColor);
    vec3 diffuseColor = gamma(diffuseColor);

    Rect light = getRect();
    vec3 points[4];
    getRectPoints(light, points);

    Plane floor;
    floor.form = vec4(0, 1, 0, 0);

    Ray ray = getRayFromCamera();

    vec3 hitPoint, N, V;
    bool isHitFloor = rayIntersectPlane(ray, floor, hitPoint);
    vec3 color;
    if(isHitFloor) {
      N = floor.form.xyz;
      V = -ray.direction;
      color = calColor(hitPoint, N, V, points);
  }

  if(rayIntersectRect(ray, light, hitPoint))
    color = lightColor;


  gl_FragColor = vec4(color, 1);
} 